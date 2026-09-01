using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Assets.Domain;

namespace Itms.IntegrationTests.AssetsModule;

/// <summary>
/// The five lifecycle operations SPEC.md §3 names, over the wire.
/// </summary>
/// <remarks>
/// WP-2.2's done-criterion is asserted here in full: transferring between two users
/// produces exactly one history entry carrying both parties, and the asset reads correctly
/// afterwards. Until WP-2.5 and WP-2.7 build the user pages, "both user pages read
/// correctly" is proved as the human directed — <c>GET /assets/{id}</c> shows the new
/// holder, and the timeline shows the move from the old one to them.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class AssetLifecycleEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>WP-2.2's done-criterion, end to end.</summary>
    [Fact]
    public async Task Transferring_between_two_users_writes_one_entry_naming_both()
    {
        using var tech = await SignedInAsync("tech");
        var alice = await UserIdAsync("user");
        var bob = await UserIdAsync("admin");
        var asset = await IssuedAsync(tech, "LAP-0300", alice);

        var transfer = await AssetsClient.AssignAsync(tech, asset.Id, bob, Token, note: "moved to the help desk");
        transfer.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The asset reads correctly afterwards: it is Bob's, and it did not restart its life.
        var (after, _) = await AssetsClient.GetAssetAsync(tech, asset.Id, Token);
        after.AssignedToUserId.ShouldBe(bob);
        after.AssetStatusCode.ShouldBe(AssetStatusCode.Deployed);

        // Exactly one entry for the transfer, and it names both parties.
        var history = await AssetsClient.HistoryAsync(tech, asset.Id, Token);
        var moves = history.Items.Where(entry => entry.Note == "moved to the help desk").ToList();

        var move = moves.ShouldHaveSingleItem();
        move.Kind.ShouldBe(nameof(AssetChangeKind.Assignment));
        move.FromValue.ShouldNotBeNullOrWhiteSpace();
        move.ToValue.ShouldNotBeNullOrWhiteSpace();
        move.FromValue.ShouldNotBe(move.ToValue);
        move.ActorId.ShouldNotBeNull();
    }

    /// <summary>
    /// The first issue is two facts and writes two entries at one instant, ordered by the
    /// sequence ordinal: who has it, then where it is in its life.
    /// </summary>
    [Fact]
    public async Task Issuing_an_in_stock_asset_writes_the_assignment_and_the_deployment()
    {
        using var tech = await SignedInAsync("tech");
        var alice = await UserIdAsync("user");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0301", typeId, Token);

        var response = await AssetsClient.AssignAsync(tech, created.Id, alice, Token, note: "onboarding");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var issued = await ApiClient.ReadAsync<AssetDto>(response, Token);
        issued.AssignedToUserId.ShouldBe(alice);
        issued.AssignedToUserName.ShouldNotBeNullOrWhiteSpace();
        issued.AssetStatusCode.ShouldBe(AssetStatusCode.Deployed);

        var history = await AssetsClient.HistoryAsync(tech, created.Id, Token);
        history.Total.ShouldBe(2);

        // Newest first, so the status entry — sequence 1 — comes back first.
        history.Items[0].Kind.ShouldBe(nameof(AssetChangeKind.Status));
        history.Items[0].Sequence.ShouldBe(1);
        history.Items[0].FromValue.ShouldBe("In Stock");
        history.Items[0].ToValue.ShouldBe("Deployed");

        history.Items[1].Kind.ShouldBe(nameof(AssetChangeKind.Assignment));
        history.Items[1].Sequence.ShouldBe(0);
        history.Items[1].FromValue.ShouldBeNull();

        // One operation, one instant, and the note on both entries.
        history.Items[0].OccurredAt.ShouldBe(history.Items[1].OccurredAt);
        history.Items.ShouldAllBe(entry => entry.Note == "onboarding");
    }

    [Fact]
    public async Task Returning_a_deployed_asset_puts_it_back_in_stock()
    {
        using var tech = await SignedInAsync("tech");
        var alice = await UserIdAsync("user");
        var asset = await IssuedAsync(tech, "LAP-0302", alice);

        var response = await AssetsClient.AssignAsync(tech, asset.Id, null, Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var returned = await ApiClient.ReadAsync<AssetDto>(response, Token);
        returned.AssignedToUserId.ShouldBeNull();
        returned.AssignedToUserName.ShouldBeNull();
        returned.AssetStatusCode.ShouldBe(AssetStatusCode.InStock);
    }

    /// <summary>
    /// Repair keeps the holder, and return to service puts the asset back where the holder
    /// implies — which is the whole reason the holder is kept.
    /// </summary>
    [Fact]
    public async Task An_asset_repaired_for_its_holder_comes_back_deployed()
    {
        using var tech = await SignedInAsync("tech");
        var alice = await UserIdAsync("user");
        var asset = await IssuedAsync(tech, "LAP-0303", alice);

        var repair = await AssetsClient.SendForRepairAsync(tech, asset.Id, Token, note: "screen flicker");
        repair.StatusCode.ShouldBe(HttpStatusCode.OK);

        var away = await ApiClient.ReadAsync<AssetDto>(repair, Token);
        away.AssetStatusCode.ShouldBe(AssetStatusCode.Repair);
        away.AssignedToUserId.ShouldBe(alice);

        var back = await AssetsClient.ReturnToServiceAsync(tech, asset.Id, Token, note: "panel replaced");
        back.StatusCode.ShouldBe(HttpStatusCode.OK);

        var home = await ApiClient.ReadAsync<AssetDto>(back, Token);
        home.AssetStatusCode.ShouldBe(AssetStatusCode.Deployed);
        home.AssignedToUserId.ShouldBe(alice);
    }

    [Fact]
    public async Task An_asset_repaired_for_nobody_comes_back_into_stock()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0304", typeId, Token);

        (await AssetsClient.SendForRepairAsync(tech, created.Id, Token)).EnsureSuccessStatusCode();
        var back = await AssetsClient.ReturnToServiceAsync(tech, created.Id, Token);

        back.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadAsync<AssetDto>(back, Token)).AssetStatusCode.ShouldBe(AssetStatusCode.InStock);
    }

    /// <summary>Retiring releases the holder, at the human's direction (WP-2.2).</summary>
    [Fact]
    public async Task Retiring_a_deployed_asset_releases_its_holder_and_records_both()
    {
        using var tech = await SignedInAsync("tech");
        var alice = await UserIdAsync("user");
        var asset = await IssuedAsync(tech, "LAP-0305", alice);

        var response = await AssetsClient.RetireAsync(tech, asset.Id, Token, note: "written off after spill");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var retired = await ApiClient.ReadAsync<AssetDto>(response, Token);
        retired.AssetStatusCode.ShouldBe(AssetStatusCode.Retired);
        retired.AssignedToUserId.ShouldBeNull();

        var history = await AssetsClient.HistoryAsync(tech, asset.Id, Token);
        var retirement = history.Items.Where(entry => entry.Note == "written off after spill").ToList();

        retirement.Count.ShouldBe(2);
        retirement.Select(entry => entry.Kind).ShouldBe(
            [nameof(AssetChangeKind.Status), nameof(AssetChangeKind.Assignment)]);
    }

    /// <summary>
    /// Retired is terminal. A mistaken retirement has no route back through this surface,
    /// which is the call the human made rather than inventing recovery semantics.
    /// </summary>
    [Fact]
    public async Task A_retired_asset_refuses_every_further_lifecycle_call()
    {
        using var tech = await SignedInAsync("tech");
        var alice = await UserIdAsync("user");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0306", typeId, Token);

        (await AssetsClient.RetireAsync(tech, created.Id, Token)).EnsureSuccessStatusCode();

        var assign = await AssetsClient.AssignAsync(tech, created.Id, alice, Token);
        assign.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(assign, Token)).Code.ShouldBe("assets.asset_not_assignable");

        var repair = await AssetsClient.SendForRepairAsync(tech, created.Id, Token);
        repair.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(repair, Token)).Code.ShouldBe("assets.transition_not_allowed");

        (await AssetsClient.ReturnToServiceAsync(tech, created.Id, Token)).StatusCode
            .ShouldBe(HttpStatusCode.Conflict);
        (await AssetsClient.RetireAsync(tech, created.Id, Token)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Issuing_to_the_person_who_already_holds_it_is_a_409()
    {
        using var tech = await SignedInAsync("tech");
        var alice = await UserIdAsync("user");
        var asset = await IssuedAsync(tech, "LAP-0307", alice);

        var again = await AssetsClient.AssignAsync(tech, asset.Id, alice, Token);

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(again, Token)).Code.ShouldBe("assets.already_assigned_to_that_user");
    }

    [Fact]
    public async Task Taking_back_an_asset_nobody_holds_is_a_409()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0308", typeId, Token);

        var response = await AssetsClient.AssignAsync(tech, created.Id, null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("assets.asset_not_assigned");
    }

    [Fact]
    public async Task Issuing_to_somebody_who_does_not_exist_is_a_400_on_the_field()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0309", typeId, Token);

        var response = await AssetsClient.AssignAsync(tech, created.Id, Guid.CreateVersion7(), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("assets.holder_not_found");
        problem.Errors.ShouldNotBeNull().ShouldContainKey("assignedToUserId");
    }

    /// <summary>
    /// Equipment is issued to end users — that is what equipment is for. Unlike a ticket
    /// assignee (WP-1.6), the holder needs no role.
    /// </summary>
    [Fact]
    public async Task An_end_user_can_be_given_equipment()
    {
        using var tech = await SignedInAsync("tech");
        var user = await UserIdAsync("user");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0310", typeId, Token);

        var response = await AssetsClient.AssignAsync(tech, created.Id, user, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadAsync<AssetDto>(response, Token)).AssignedToUserId.ShouldBe(user);
    }

    /// <summary>SPEC.md §14 keeps the inventory on the operational surface.</summary>
    [Fact]
    public async Task An_end_user_cannot_move_an_asset_through_its_lifecycle()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0311", typeId, Token);

        using var user = await SignedInAsync("user");

        (await AssetsClient.AssignAsync(user, created.Id, null, Token)).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
        (await AssetsClient.SendForRepairAsync(user, created.Id, Token)).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
        (await AssetsClient.RetireAsync(user, created.Id, Token)).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_lifecycle_call_against_an_unknown_asset_is_a_404()
    {
        using var tech = await SignedInAsync("tech");

        var response = await AssetsClient.SendForRepairAsync(tech, Guid.CreateVersion7(), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("assets.asset_not_found");
    }

    /// <summary>
    /// The deployment retired its own Deployed status. Issuing out of stock then has
    /// nowhere to move the asset, and guessing which of the remaining statuses to use would
    /// be inventing policy — the same call WP-2.1 made for a missing In Stock.
    /// </summary>
    [Fact]
    public async Task Issuing_out_of_stock_with_no_active_deployed_status_is_a_409()
    {
        using var tech = await SignedInAsync("tech");
        using var admin = await SignedInAsync("admin");

        var alice = await UserIdAsync("user");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0312", typeId, Token);

        var deployed = await AssetsClient.StatusByCodeAsync(admin, AssetStatusCode.Deployed, Token);
        var retire = await ApiClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"{AssetsClient.Statuses}/{deployed.Id}/deactivate",
            null,
            Token);
        retire.EnsureSuccessStatusCode();

        var response = await AssetsClient.AssignAsync(tech, created.Id, alice, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("assets.missing_lifecycle_status");

        // And nothing was written: the asset is untouched and its timeline is empty.
        var (untouched, _) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);
        untouched.AssignedToUserId.ShouldBeNull();
        untouched.AssetStatusCode.ShouldBe(AssetStatusCode.InStock);
        (await AssetsClient.HistoryAsync(tech, created.Id, Token)).Total.ShouldBe(0);
    }

    /// <summary>
    /// A custom status is not in the lifecycle table, so a transition out of it is refused
    /// — but the equipment in it is still issuable, because assignment is a separate fact.
    /// </summary>
    [Fact]
    public async Task An_asset_in_a_custom_status_can_be_issued_but_not_transitioned()
    {
        using var tech = await SignedInAsync("tech");
        using var admin = await SignedInAsync("admin");

        var alice = await UserIdAsync("user");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var onLoan = await AssetsClient.CreateStatusAsync(admin, "on-loan", "On Loan", 500, Token);

        var response = await AssetsClient.PostAssetAsync(
            tech,
            new { assetTag = "LAP-0313", assetTypeId = typeId, assetStatusId = onLoan.Id },
            Token);
        response.EnsureSuccessStatusCode();
        var created = await ApiClient.ReadAsync<AssetDto>(response, Token);

        var assign = await AssetsClient.AssignAsync(tech, created.Id, alice, Token);
        assign.StatusCode.ShouldBe(HttpStatusCode.OK);

        var issued = await ApiClient.ReadAsync<AssetDto>(assign, Token);
        issued.AssignedToUserId.ShouldBe(alice);
        issued.AssetStatusCode.ShouldBe("on-loan");

        (await AssetsClient.SendForRepairAsync(tech, created.Id, Token)).StatusCode
            .ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_note_longer_than_the_column_is_a_400()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0314", typeId, Token);

        var response = await AssetsClient.SendForRepairAsync(
            tech,
            created.Id,
            Token,
            note: new string('x', AssetHistoryEntry.NoteMaxLength + 1));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Errors.ShouldNotBeNull().ShouldContainKey("note");
    }

    private static async Task<AssetDto> IssuedAsync(HttpClient client, string assetTag, Guid holderId)
    {
        var typeId = await AssetsClient.AnyTypeIdAsync(client, Token);
        var created = await AssetsClient.CreateAssetAsync(client, assetTag, typeId, Token);

        var response = await AssetsClient.AssignAsync(client, created.Id, holderId, Token);
        response.EnsureSuccessStatusCode();

        return await ApiClient.ReadAsync<AssetDto>(response, Token);
    }

    private async Task<Guid> UserIdAsync(string userName)
    {
        using var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        return (await AuthClient.ReadUserAsync(response, Token)).Id;
    }

    private Task<HttpClient> SignedInAsync(string userName) =>
        AuthClient.SignedInAsync(fixture, userName, Token);
}
