using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.AssetsModule;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Assets.Domain;

namespace Itms.IntegrationTests.AuditModule;

/// <summary>
/// SPEC.md §15 makes asset modifications and administrative configuration changes both
/// mandatory audit coverage. Nothing in WP-2.1 raises a domain event — ARCHITECTURE.md §5
/// names <c>AssetAssigned</c> and <c>AssetStatusChanged</c>, which belong to WP-2.2's
/// transitions — so every row here is written through <c>IAuditWriter</c> inside the
/// request, and arrives before the response does.
/// </summary>
/// <remarks>
/// <b>Creation and reference-data rows are synchronous; assignment and lifecycle rows are
/// not.</b> WP-2.2 started publishing <c>AssetAssigned</c> and <c>AssetStatusChanged</c>,
/// so those two are derived by the Audit module's consumer one dispatcher pass later and
/// every assertion on them waits with <c>Eventually</c> — exactly as WP-1.6's ticket rows
/// do. Everything else here still arrives before the response does, because it goes through
/// <c>IAuditWriter</c> inside the request.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class AssetsAuditTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Recording_an_asset_is_audited_against_the_account_that_did_it()
    {
        var (client, techId) = await SignedInAsync("tech");
        using var tech = client;

        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var asset = await AssetsClient.CreateAssetAsync(tech, "LAP-0200", typeId, Token);

        var row = (await Entries("Asset", asset.Id.ToString())).ShouldHaveSingleItem();

        row.Action.ShouldBe("assets.asset_created");
        row.ActorId.ShouldBe(techId);
        row.ActorName.ShouldNotBeNullOrWhiteSpace();
        row.SourceIp.ShouldBe(IdentityWebFixture.RemoteIpAddress);
        row.Changes["assetTag"].ShouldBe(new(null, "LAP-0200"));
        row.Changes["assetTypeId"].ShouldBe(new(null, typeId.ToString()));
    }

    /// <summary>
    /// The refusal writes nothing. An entry claiming an asset was created, beside no asset,
    /// would make the trail lie — and the write happens inside the transaction precisely so
    /// a rollback takes the entry with it.
    /// </summary>
    [Fact]
    public async Task A_refused_duplicate_tag_writes_no_audit_row()
    {
        var (client, _) = await SignedInAsync("tech");
        using var tech = client;

        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        await AssetsClient.CreateAssetAsync(tech, "LAP-0201", typeId, Token);

        var before = await ByAction("assets.asset_created");

        var response = await AssetsClient.PostAssetAsync(
            tech,
            new { assetTag = "LAP-0201", assetTypeId = typeId },
            Token);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await ByAction("assets.asset_created")).Count.ShouldBe(before.Count);
    }

    /// <summary>
    /// WP-2.6b's one new action. An edit raises no domain event — it cannot touch the tag,
    /// the status, or the holder — so <c>IAuditWriter</c> is the route, and the row arrives
    /// inside the request like the create's.
    /// </summary>
    [Fact]
    public async Task Correcting_an_asset_is_audited_with_the_fields_that_moved()
    {
        var (client, techId) = await SignedInAsync("tech");
        using var tech = client;

        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var asset = await AssetsClient.CreateAssetAsync(tech, "LAP-0210", typeId, Token);

        var response = await AssetsClient.PutAssetAsync(
            tech,
            asset.Id,
            new { assetTypeId = typeId, name = "Reception desktop", vendor = "Insight" },
            Token);
        response.EnsureSuccessStatusCode();

        var row = (await Entries("Asset", asset.Id.ToString()))
            .Single(entry => string.Equals(entry.Action, "assets.asset_updated", StringComparison.Ordinal));

        row.ActorId.ShouldBe(techId);
        row.SourceIp.ShouldBe(IdentityWebFixture.RemoteIpAddress);
        row.Changes["name"].ShouldBe(new(null, "Reception desktop"));
        row.Changes["vendor"].ShouldBe(new(null, "Insight"));
    }

    /// <summary>
    /// ARCHITECTURE.md §8 asks for changed fields only, so a field the edit left where it
    /// was is not in the diff — otherwise a form that posts all thirteen would make every
    /// correction look like a rewrite of the row.
    /// </summary>
    [Fact]
    public async Task An_edit_records_only_what_moved_and_records_both_sides()
    {
        var (client, _) = await SignedInAsync("tech");
        using var tech = client;

        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var asset = await AssetsClient.CreateDetailedAsync(
            tech,
            new { assetTag = "LAP-0211", assetTypeId = typeId, name = "Reception desktop", vendor = "Insight" },
            Token);

        (await AssetsClient.PutAssetAsync(
            tech,
            asset.Id,
            new { assetTypeId = typeId, name = "Front desk PC", vendor = "Insight" },
            Token)).EnsureSuccessStatusCode();

        var row = (await Entries("Asset", asset.Id.ToString()))
            .Single(entry => string.Equals(entry.Action, "assets.asset_updated", StringComparison.Ordinal));

        row.Changes["name"].ShouldBe(new("Reception desktop", "Front desk PC"));
        row.Changes.ShouldNotContainKey("vendor");
        row.Changes.ShouldNotContainKey("assetTypeId");
    }

    /// <summary>
    /// An edit that moves nothing writes no row: the entity leaves the asset untouched, so
    /// an entry would be claiming a change the database cannot show.
    /// </summary>
    [Fact]
    public async Task An_edit_that_moves_nothing_writes_no_audit_row()
    {
        var (client, _) = await SignedInAsync("tech");
        using var tech = client;

        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var asset = await AssetsClient.CreateDetailedAsync(
            tech,
            new { assetTag = "LAP-0212", assetTypeId = typeId, name = "Reception desktop" },
            Token);

        (await AssetsClient.PutAssetAsync(
            tech,
            asset.Id,
            new { assetTypeId = typeId, name = "Reception desktop" },
            Token)).EnsureSuccessStatusCode();

        (await ByAction("assets.asset_updated"))
            .ShouldNotContain(entry => string.Equals(entry.EntityId, asset.Id.ToString(), StringComparison.Ordinal));
    }

    /// <summary>
    /// A refused edit writes nothing, for the reason a refused create does: the write is
    /// inside the transaction, so a rollback takes the entry with it.
    /// </summary>
    [Fact]
    public async Task A_refused_edit_writes_no_audit_row()
    {
        var (client, _) = await SignedInAsync("tech");
        using var tech = client;

        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        await AssetsClient.CreateDetailedAsync(
            tech,
            new { assetTag = "LAP-0213", assetTypeId = typeId, manufacturer = "HP", serialNumber = "CND777" },
            Token);
        var asset = await AssetsClient.CreateAssetAsync(tech, "LAP-0214", typeId, Token);

        var response = await AssetsClient.PutAssetAsync(
            tech,
            asset.Id,
            new { assetTypeId = typeId, manufacturer = "HP", serialNumber = "CND777" },
            Token);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await Entries("Asset", asset.Id.ToString()))
            .ShouldNotContain(entry => string.Equals(entry.Action, "assets.asset_updated", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Creating_a_status_records_its_code()
    {
        var (client, _) = await SignedInAsync("admin");
        using var admin = client;

        var status = await AssetsClient.CreateStatusAsync(admin, "on-loan", "On Loan", 500, Token);

        var row = (await Entries("AssetStatus", status.Id.ToString())).ShouldHaveSingleItem();

        row.Action.ShouldBe("assets.asset_status_created");
        row.Changes["code"].ShouldBe(new(null, "on-loan"));
        row.Changes["name"].ShouldBe(new(null, "On Loan"));
    }

    [Fact]
    public async Task Retiring_and_reinstating_a_type_are_separate_actions()
    {
        var (client, _) = await SignedInAsync("admin");
        using var admin = client;

        var type = await AssetsClient.CreateTypeAsync(admin, "Scanner", 500, Token);

        await Post(admin, $"{AssetsClient.Types}/{type.Id}/deactivate");
        await Post(admin, $"{AssetsClient.Types}/{type.Id}/reactivate");

        var rows = await Entries("AssetType", type.Id.ToString());

        rows.Select(row => row.Action).ShouldBe([
            "assets.asset_type_created",
            "assets.asset_type_retired",
            "assets.asset_type_reinstated",
        ]);
        rows[1].Changes["isActive"].ShouldBe(new("true", "false"));
        rows[2].Changes["isActive"].ShouldBe(new("false", "true"));
    }

    /// <summary>Only the fields that moved, per ARCHITECTURE.md §8.</summary>
    [Fact]
    public async Task Renaming_a_type_records_only_the_name()
    {
        var (client, _) = await SignedInAsync("admin");
        using var admin = client;

        var type = await AssetsClient.CreateTypeAsync(admin, "Scanner", 500, Token);

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{AssetsClient.Types}/{type.Id}",
            new { name = "Document Scanner", description = (string?)null, sortOrder = 500 },
            Token);
        response.EnsureSuccessStatusCode();

        var update = (await Entries("AssetType", type.Id.ToString()))[^1];

        update.Action.ShouldBe("assets.asset_type_updated");
        update.Changes["name"].ShouldBe(new("Scanner", "Document Scanner"));
        update.Changes.ShouldNotContainKey("sortOrder");
        update.Changes.ShouldNotContainKey("description");
    }


    /// <summary>
    /// The trap WP-2.1 recorded and this package had to not walk into. The assignment is
    /// audited from the event and <em>only</em> from the event: an <c>IAuditWriter</c> call
    /// beside the publish would put two rows here saying the same thing, which is what
    /// WP-1.6 had to go back and delete from Helpdesk.
    /// </summary>
    [Fact]
    public async Task Issuing_an_asset_writes_exactly_one_assignment_row_and_one_status_row()
    {
        var (client, techId) = await SignedInAsync("tech");
        using var tech = client;
        var holder = await UserIdAsync("user");

        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var asset = await AssetsClient.CreateAssetAsync(tech, "LAP-0600", typeId, Token);

        (await AssetsClient.AssignAsync(tech, asset.Id, holder, Token)).EnsureSuccessStatusCode();

        await Eventually.UntilAsync(
            async () => (await Entries("Asset", asset.Id.ToString())).Count == 3,
            "the assignment and status rows to be dispatched",
            Token);

        var rows = await Entries("Asset", asset.Id.ToString());

        rows.Select(row => row.Action).ShouldBe([
            "assets.asset_created",
            "asset.assigned",
            "asset.status_changed",
        ]);

        var assigned = rows[1];
        assigned.Changes["assignedToUserId"].ShouldBe(new(null, holder.ToString()));

        // Stamped explicitly by the handler, because the dispatcher runs on a background
        // scope with no principal.
        assigned.ActorId.ShouldBe(techId);
    }

    /// <summary>
    /// The codes, not the display names. An administrator renaming "Repair" must not make
    /// two rows written a year apart describe the same move differently.
    /// </summary>
    [Fact]
    public async Task A_lifecycle_move_is_audited_by_status_code()
    {
        var (client, _) = await SignedInAsync("tech");
        using var tech = client;

        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var asset = await AssetsClient.CreateAssetAsync(tech, "LAP-0601", typeId, Token);

        (await AssetsClient.SendForRepairAsync(tech, asset.Id, Token)).EnsureSuccessStatusCode();

        await Eventually.UntilAsync(
            async () => (await ByAction("asset.status_changed")).Count == 1,
            "the status row to be dispatched",
            Token);

        var row = (await ByAction("asset.status_changed")).ShouldHaveSingleItem();

        row.EntityId.ShouldBe(asset.Id.ToString());
        row.Changes["status"].ShouldBe(new(AssetStatusCode.InStock, AssetStatusCode.Repair));
    }

    /// <summary>
    /// A transfer moves nobody's lifecycle status, so it must not raise
    /// <c>AssetStatusChanged</c> — a row saying an asset went from Deployed to Deployed is
    /// the mistake WP-1.6 documented for a reassignment.
    /// </summary>
    [Fact]
    public async Task A_transfer_writes_an_assignment_row_and_no_status_row()
    {
        var (client, _) = await SignedInAsync("tech");
        using var tech = client;
        var alice = await UserIdAsync("user");
        var bob = await UserIdAsync("admin");

        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var asset = await AssetsClient.CreateAssetAsync(tech, "LAP-0602", typeId, Token);

        (await AssetsClient.AssignAsync(tech, asset.Id, alice, Token)).EnsureSuccessStatusCode();
        (await AssetsClient.AssignAsync(tech, asset.Id, bob, Token)).EnsureSuccessStatusCode();

        await Eventually.UntilAsync(
            async () => (await ByAction("asset.assigned")).Count == 2,
            "both assignment rows to be dispatched",
            Token);

        // The issue moved the status out of stock; the transfer moved nothing.
        (await ByAction("asset.status_changed")).ShouldHaveSingleItem();

        var transfer = (await ByAction("asset.assigned"))[^1];
        transfer.Changes["assignedToUserId"].ShouldBe(new(alice.ToString(), bob.ToString()));
    }

    /// <summary>
    /// The price WP-1.6 paid for defusing the double-write, restated for assets so nobody
    /// reads it as a defect. An event-derived row has no source IP and no actor name,
    /// because the dispatcher runs on a background scope. The asset's own timeline keeps
    /// both, which is where somebody chasing a laptop should look.
    /// </summary>
    [Fact]
    public async Task An_event_derived_row_carries_no_source_ip_and_no_actor_name()
    {
        var (client, techId) = await SignedInAsync("tech");
        using var tech = client;
        var holder = await UserIdAsync("user");

        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var asset = await AssetsClient.CreateAssetAsync(tech, "LAP-0603", typeId, Token);

        (await AssetsClient.AssignAsync(tech, asset.Id, holder, Token)).EnsureSuccessStatusCode();

        await Eventually.UntilAsync(
            async () => (await ByAction("asset.assigned")).Count == 1,
            "the assignment row to be dispatched",
            Token);

        var row = (await ByAction("asset.assigned")).ShouldHaveSingleItem();
        row.ActorId.ShouldBe(techId);
        row.ActorName.ShouldBeNull();
        row.SourceIp.ShouldBeNull();

        // Which is why the timeline exists: it was written inside the request and kept both.
        var history = await AssetsClient.HistoryAsync(tech, asset.Id, Token);
        var entry = history.Items.First(item => item.Kind == "Assignment");
        entry.ActorId.ShouldBe(techId);
        entry.ActorName.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// The refusal writes nothing, on the lifecycle surface as well as on creation: no
    /// event is published from a transaction that rolled back.
    /// </summary>
    [Fact]
    public async Task A_refused_lifecycle_move_publishes_nothing()
    {
        var (client, _) = await SignedInAsync("tech");
        using var tech = client;

        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var asset = await AssetsClient.CreateAssetAsync(tech, "LAP-0604", typeId, Token);

        (await AssetsClient.RetireAsync(tech, asset.Id, Token)).EnsureSuccessStatusCode();

        await Eventually.UntilAsync(
            async () => (await ByAction("asset.status_changed")).Count == 1,
            "the retirement row to be dispatched",
            Token);

        // Retired is terminal, so this is refused before anything is written.
        (await AssetsClient.SendForRepairAsync(tech, asset.Id, Token)).StatusCode
            .ShouldBe(HttpStatusCode.Conflict);

        (await ByAction("asset.status_changed")).ShouldHaveSingleItem();
    }

    private async Task<Guid> UserIdAsync(string userName)
    {
        using var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        return (await AuthClient.ReadUserAsync(response, Token)).Id;
    }

    private static async Task Post(HttpClient client, string path)
    {
        var response = await ApiClient.SendAsync(client, HttpMethod.Post, path, null, Token);
        response.EnsureSuccessStatusCode();
    }

    private Task<IReadOnlyList<AuditRow>> Entries(string entityType, string entityId) =>
        AuditQueries.ByEntityAsync(fixture.DataSource, entityType, entityId, Token);

    private Task<IReadOnlyList<AuditRow>> ByAction(string action) =>
        AuditQueries.ByActionAsync(fixture.DataSource, action, Token);

    private async Task<(HttpClient Client, Guid UserId)> SignedInAsync(string userName)
    {
        var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        var user = await AuthClient.ReadUserAsync(response, Token);
        return (client, user.Id);
    }
}
