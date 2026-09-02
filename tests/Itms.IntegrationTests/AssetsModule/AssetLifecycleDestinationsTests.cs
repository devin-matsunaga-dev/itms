using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Assets.Domain;

namespace Itms.IntegrationTests.AssetsModule;

/// <summary>
/// The two derived fields WP-2.6b added to <c>AssetResponse</c>, over the wire.
/// </summary>
/// <remarks>
/// <para>
/// WP-2.6b's done-criterion is that the lifecycle actions read the server's legal
/// destinations rather than restating <c>AssetLifecycle</c>'s table in TypeScript, and that
/// an illegal action is <em>absent</em> rather than disabled. That is only true for as long
/// as the field is right, which is what this class holds.
/// </para>
/// <para>
/// <c>canBeAssigned</c> is here because the destination list cannot answer for assignment:
/// it is empty both from a terminal status and from a custom one, and those two differ —
/// <c>Asset.AssignTo</c> refuses only the terminal three, deliberately, so that adding a
/// status does not quietly make the equipment in it unissuable. The last test in this class
/// is the one that would fail if the client ever tried to infer one from the other.
/// </para>
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class AssetLifecycleDestinationsTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_new_asset_offers_the_moves_legal_from_stock()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0700", typeId, Token);

        // The create's own reply carries them, so a client that records equipment and goes
        // straight to its screen never has to read the asset back to know what it may do.
        created.AllowedNextStatusCodes.ShouldBe(
            [AssetStatusCode.Deployed, AssetStatusCode.Repair, AssetStatusCode.Retired],
            ignoreOrder: true);
        created.CanBeAssigned.ShouldBeTrue();

        var (read, _) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);
        read.AllowedNextStatusCodes.ShouldBe(created.AllowedNextStatusCodes, ignoreOrder: true);
    }

    /// <summary>
    /// They move with the asset. A lifecycle response carries the list for where the asset
    /// now stands, so the buttons on the screen are right the moment the write returns
    /// rather than one refetch later.
    /// </summary>
    [Fact]
    public async Task A_lifecycle_response_carries_the_destinations_from_where_the_asset_now_stands()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0701", typeId, Token);

        var repaired = await AssetsClient.SendForRepairAsync(tech, created.Id, Token);
        repaired.EnsureSuccessStatusCode();

        var inRepair = await ApiClient.ReadAsync<AssetDto>(repaired, Token);
        inRepair.AssetStatusCode.ShouldBe(AssetStatusCode.Repair);
        inRepair.AllowedNextStatusCodes.ShouldBe(
            [AssetStatusCode.Deployed, AssetStatusCode.InStock, AssetStatusCode.Retired],
            ignoreOrder: true);

        // A status is never a destination from itself: moving an asset to where it already
        // is would raise an event and write a history line saying it went from Repair to
        // Repair, which is why AssetLifecycle refuses it.
        inRepair.AllowedNextStatusCodes.ShouldNotContain(AssetStatusCode.Repair);
    }

    /// <summary>
    /// The three terminal statuses have no way out (WP-2.2, at the human's direction), so a
    /// retired asset offers nothing — which is what makes every lifecycle action on its
    /// screen absent rather than disabled.
    /// </summary>
    [Fact]
    public async Task A_retired_asset_offers_nothing_and_cannot_be_assigned()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0702", typeId, Token);

        (await AssetsClient.RetireAsync(tech, created.Id, Token)).EnsureSuccessStatusCode();

        var (retired, _) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);
        retired.AssetStatusCode.ShouldBe(AssetStatusCode.Retired);
        retired.AllowedNextStatusCodes.ShouldBeEmpty();
        retired.CanBeAssigned.ShouldBeFalse();
    }

    /// <summary>
    /// The reason <c>canBeAssigned</c> exists. An administrator's own status is not in the
    /// lifecycle table, so it offers no destinations — but it is not terminal, and equipment
    /// in it is still issuable. A client that inferred assignability from an empty
    /// destination list would get this case exactly backwards.
    /// </summary>
    [Fact]
    public async Task A_custom_status_offers_no_destinations_but_the_asset_is_still_assignable()
    {
        using var admin = await SignedInAsync("admin");
        var typeId = await AssetsClient.AnyTypeIdAsync(admin, Token);
        var loaned = await AssetsClient.CreateStatusAsync(admin, "on-loan", "On Loan", 70, Token);

        var created = await AssetsClient.CreateDetailedAsync(
            admin,
            new { assetTag = "LAP-0703", assetTypeId = typeId, assetStatusId = loaned.Id },
            Token);

        created.AssetStatusCode.ShouldBe("on-loan");
        created.AllowedNextStatusCodes.ShouldBeEmpty();
        created.CanBeAssigned.ShouldBeTrue();

        // And the server agrees with the field it sent: the assignment is accepted.
        var alice = await UserIdAsync("user");
        var assigned = await AssetsClient.AssignAsync(admin, created.Id, alice, Token);
        assigned.EnsureSuccessStatusCode();
        (await ApiClient.ReadAsync<AssetDto>(assigned, Token)).CanBeAssigned.ShouldBeTrue();
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
