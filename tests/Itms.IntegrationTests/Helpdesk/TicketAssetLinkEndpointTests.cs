using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.AssetsModule;
using Itms.IntegrationTests.AuditModule;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// The ticket ↔ asset link: the join WP-2.5 exists for, end to end through the real
/// endpoints and the real <c>IAssetLookup</c>.
/// </summary>
/// <remarks>
/// The assertion that matters most is the last one in each direction — that the link is
/// recorded in the timeline and in the audit trail with the asset's <em>tag</em>, not with
/// a bare id, and that a refused link writes neither.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketAssetLinkEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Guid? _departmentId;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();

        // The truncate takes the department with it, so the cached id would name a row
        // that no longer exists.
        _departmentId = null;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// The whole point of the package: a ticket names a machine, and the detail read shows
    /// which one without the client resolving anything.
    /// </summary>
    [Fact]
    public async Task Linking_an_asset_puts_it_on_the_ticket_and_on_the_detail()
    {
        using var tech = await SignedInAsync("tech");
        var asset = await AssetAsync(tech, "LAP-0500");
        var ticket = await TicketAsync(tech, "Laptop will not charge");

        var (link, etag) = await TicketClient.LinksAssetAsync(tech, ticket.Id, asset.Id, Token);

        link.PreviousAsset.ShouldBeNull();
        link.RelatedAsset.ShouldNotBeNull();
        link.RelatedAsset.AssetTag.ShouldBe("LAP-0500");
        etag.ShouldNotBeNullOrWhiteSpace();

        var (read, _) = await TicketClient.GetAsync(tech, ticket.Id, Token);

        read.RelatedAssetId.ShouldBe(asset.Id);
        read.RelatedAsset.ShouldNotBeNull();
        read.RelatedAsset.AssetTag.ShouldBe("LAP-0500");
        read.RelatedAsset.Status.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// The decision this package took: the display text is read live rather than cached on
    /// the ticket, so renaming what the asset is reaches every ticket already filed against
    /// it. A cached column would have gone stale here — which is exactly what
    /// <c>departmentName</c> does, and why STATUS.md records that as a gap.
    /// </summary>
    [Fact]
    public async Task The_ticket_shows_the_asset_as_it_reads_now_not_as_it_read_when_linked()
    {
        using var tech = await SignedInAsync("tech");
        using var admin = await SignedInAsync("admin");

        var type = await AssetsClient.CreateTypeAsync(admin, "Field Laptop", 90, Token);
        var asset = await AssetsClient.CreateAssetAsync(tech, "LAP-0501", type.Id, Token);
        var ticket = await TicketAsync(tech, "Screen flickers");

        await TicketClient.LinksAssetAsync(tech, ticket.Id, asset.Id, Token);

        var (before, _) = await TicketClient.GetAsync(tech, ticket.Id, Token);
        before.RelatedAsset!.AssetType.ShouldBe("Field Laptop");

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{AssetsClient.Types}/{type.Id}",
            new { name = "Rugged Field Laptop", description = (string?)null, sortOrder = 90 },
            Token);

        response.EnsureSuccessStatusCode();

        var (after, _) = await TicketClient.GetAsync(tech, ticket.Id, Token);
        after.RelatedAsset!.AssetType.ShouldBe("Rugged Field Laptop");
    }

    [Fact]
    public async Task Relinking_carries_both_sides_and_unlinking_clears_it()
    {
        using var tech = await SignedInAsync("tech");
        var first = await AssetAsync(tech, "LAP-0502");
        var second = await AssetAsync(tech, "LAP-0503");
        var ticket = await TicketAsync(tech, "Wrong machine");

        await TicketClient.LinksAssetAsync(tech, ticket.Id, first.Id, Token);

        var (relink, _) = await TicketClient.LinksAssetAsync(tech, ticket.Id, second.Id, Token);
        relink.PreviousAsset!.AssetTag.ShouldBe("LAP-0502");
        relink.RelatedAsset!.AssetTag.ShouldBe("LAP-0503");

        var (unlink, _) = await TicketClient.LinksAssetAsync(tech, ticket.Id, null, Token);
        unlink.PreviousAsset!.AssetTag.ShouldBe("LAP-0503");
        unlink.RelatedAsset.ShouldBeNull();

        var (read, _) = await TicketClient.GetAsync(tech, ticket.Id, Token);
        read.RelatedAssetId.ShouldBeNull();
        read.RelatedAsset.ShouldBeNull();
    }

    /// <summary>Invariant 3: the change and its timeline entry, with the tag a person can read.</summary>
    [Fact]
    public async Task The_link_is_recorded_in_the_timeline_with_the_asset_tag()
    {
        using var tech = await SignedInAsync("tech");
        var asset = await AssetAsync(tech, "LAP-0504");
        var ticket = await TicketAsync(tech, "Battery swelling");

        await TicketClient.LinksAssetAsync(tech, ticket.Id, asset.Id, Token);

        var (read, _) = await TicketClient.GetAsync(tech, ticket.Id, Token);
        var entry = read.History.ShouldHaveSingleItem();

        entry.Kind.ShouldBe(TicketChangeKind.Asset);
        entry.FromValue.ShouldBeNull();
        entry.ToValue.ShouldBe("LAP-0504");
        entry.ActorId.ShouldNotBeNull();
    }

    /// <summary>
    /// SPEC.md §15 makes a ticket modification mandatory audit coverage, and no domain
    /// event describes a link — so the handler writes the row itself.
    /// </summary>
    [Fact]
    public async Task The_link_is_audited_with_both_ids_and_both_tags()
    {
        using var tech = await SignedInAsync("tech");
        var asset = await AssetAsync(tech, "LAP-0505");
        var ticket = await TicketAsync(tech, "Fan noise");

        await TicketClient.LinksAssetAsync(tech, ticket.Id, asset.Id, Token);

        var rows = await AuditQueries.ByActionAsync(fixture.DataSource, "helpdesk.ticket_asset_linked", Token);
        var row = rows.ShouldHaveSingleItem();

        row.EntityType.ShouldBe("Ticket");
        row.EntityId.ShouldBe(ticket.Id.ToString());
        row.Changes["relatedAssetId"].Before.ShouldBeNull();
        row.Changes["relatedAssetId"].After.ShouldBe(asset.Id.ToString());
        row.Changes["relatedAssetTag"].After.ShouldBe("LAP-0505");
    }

    /// <summary>
    /// A 400 naming the field, not a 404: the ticket in the route was found, and what is
    /// wrong is a value in the body.
    /// </summary>
    [Fact]
    public async Task Linking_an_asset_that_does_not_exist_is_refused()
    {
        using var tech = await SignedInAsync("tech");
        var ticket = await TicketAsync(tech, "Ghost machine");

        var response = await TicketClient.LinkAssetAsync(tech, ticket.Id, Guid.CreateVersion7(), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.related_asset_not_found");
        problem.Errors!.ShouldContainKey("assetId");

        var (read, _) = await TicketClient.GetAsync(tech, ticket.Id, Token);
        read.RelatedAssetId.ShouldBeNull();
        read.History.ShouldBeEmpty();
    }

    [Fact]
    public async Task Linking_the_asset_the_ticket_already_names_is_a_409()
    {
        using var tech = await SignedInAsync("tech");
        var asset = await AssetAsync(tech, "LAP-0506");
        var ticket = await TicketAsync(tech, "Twice over");

        await TicketClient.LinksAssetAsync(tech, ticket.Id, asset.Id, Token);

        var response = await TicketClient.LinkAssetAsync(tech, ticket.Id, asset.Id, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code
            .ShouldBe("helpdesk.already_linked_to_that_asset");

        // Still one line: the refused call wrote nothing.
        var (read, _) = await TicketClient.GetAsync(tech, ticket.Id, Token);
        read.History.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Clearing_a_link_the_ticket_does_not_have_is_a_409()
    {
        using var tech = await SignedInAsync("tech");
        var ticket = await TicketAsync(tech, "Nothing to clear");

        var response = await TicketClient.LinkAssetAsync(tech, ticket.Id, null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code
            .ShouldBe("helpdesk.ticket_has_no_related_asset");
    }

    [Fact]
    public async Task A_ticket_that_does_not_exist_is_a_404()
    {
        using var tech = await SignedInAsync("tech");
        var asset = await AssetAsync(tech, "LAP-0507");

        var response = await TicketClient.LinkAssetAsync(tech, Guid.CreateVersion7(), asset.Id, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.ticket_not_found");
    }

    /// <summary>
    /// Two technicians on the same ticket screen must not be able to overwrite each other
    /// silently — the same precondition every other ticket write honours.
    /// </summary>
    [Fact]
    public async Task A_stale_If_Match_is_refused_before_anything_is_attempted()
    {
        using var tech = await SignedInAsync("tech");
        var first = await AssetAsync(tech, "LAP-0508");
        var second = await AssetAsync(tech, "LAP-0509");
        var ticket = await TicketAsync(tech, "Racing");

        var (_, stale) = await TicketClient.GetAsync(tech, ticket.Id, Token);
        await TicketClient.LinksAssetAsync(tech, ticket.Id, first.Id, Token);

        var response = await TicketClient.LinkAssetAsync(tech, ticket.Id, second.Id, Token, ifMatch: stale);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);

        var (read, _) = await TicketClient.GetAsync(tech, ticket.Id, Token);
        read.RelatedAssetId.ShouldBe(first.Id);
    }

    /// <summary>
    /// A requester may read and comment on their own ticket; they never decide which piece
    /// of equipment it is filed against.
    /// </summary>
    [Fact]
    public async Task An_end_user_cannot_link_an_asset()
    {
        using var tech = await SignedInAsync("tech");
        var asset = await AssetAsync(tech, "LAP-0510");

        var userId = await UserIdAsync("user");
        var ticket = await TicketAsync(tech, "Theirs", requesterId: userId);

        using var user = await SignedInAsync("user");
        var response = await TicketClient.LinkAssetAsync(user, ticket.Id, asset.Id, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<AssetDto> AssetAsync(HttpClient client, string tag)
    {
        var typeId = await AssetsClient.AnyTypeIdAsync(client, Token);
        return await AssetsClient.CreateAssetAsync(client, tag, typeId, Token);
    }

    /// <summary>
    /// The department every ticket in this class is filed against, created once.
    /// </summary>
    /// <remarks>
    /// Cached because a department name is unique and <c>ResetAsync</c> empties the tree
    /// between tests: creating one per ticket would fail the second time inside a test that
    /// raises two.
    /// </remarks>
    private async Task<Guid> DepartmentAsync()
    {
        if (_departmentId is { } existing)
        {
            return existing;
        }

        using var admin = await SignedInAsync("admin");
        var created = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        _departmentId = created;
        return created;
    }

    private async Task<TicketDetailDto> TicketAsync(HttpClient client, string subject, Guid? requesterId = null)
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await DepartmentAsync();

        return await TicketClient.CreateAsync(client, reference, departmentId, subject, Token, requesterId);
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
