using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.AssetsModule;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.AuditModule;

/// <summary>
/// SPEC.md §15 makes asset modifications and administrative configuration changes both
/// mandatory audit coverage. Nothing in WP-2.1 raises a domain event — ARCHITECTURE.md §5
/// names <c>AssetAssigned</c> and <c>AssetStatusChanged</c>, which belong to WP-2.2's
/// transitions — so every row here is written through <c>IAuditWriter</c> inside the
/// request, and arrives before the response does.
/// </summary>
/// <remarks>
/// <b>These rows are synchronous, unlike a ticket's.</b> WP-1.6 moved the ticket actions
/// onto the outbox and its suites had to start waiting with <c>Eventually</c>. If WP-2.2
/// starts publishing the two asset events, the assignment and status rows become eventually
/// consistent and any assertion on them will need the same wait — the rows in this class
/// will not, because creation stays on <c>IAuditWriter</c>.
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
