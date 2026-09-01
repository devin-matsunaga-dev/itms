using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Helpdesk;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.IntegrationTests.AssetsModule;

/// <summary>
/// An asset's support history: <c>GET /api/v1/assets/{id}/tickets</c>, read across the
/// module boundary through <c>ITicketLookup</c>.
/// </summary>
/// <remarks>
/// The point of reading it through the real host rather than the interface is that the
/// boundary is what is being tested: Assets holds no ticket table and no reference to
/// Helpdesk, and these rows still arrive.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class AssetTicketsEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
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
    /// An asset that exists and has never had a ticket answers an empty page; one that
    /// does not exist answers 404. Reading the tickets alone could not tell those apart.
    /// </summary>
    [Fact]
    public async Task An_asset_nobody_has_reported_has_an_empty_support_history()
    {
        using var tech = await SignedInAsync("tech");
        var asset = await AssetAsync(tech, "LAP-0600");

        var page = await TicketsAsync(tech, asset.Id);

        page.Total.ShouldBe(0);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_support_history_of_an_unknown_asset_is_a_404()
    {
        using var tech = await SignedInAsync("tech");

        var response = await tech.GetAsync(
            new Uri($"{AssetsClient.Assets}/{Guid.CreateVersion7()}/tickets", UriKind.Relative),
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("assets.asset_not_found");
    }

    [Fact]
    public async Task An_end_user_cannot_read_an_assets_support_history()
    {
        using var tech = await SignedInAsync("tech");
        var asset = await AssetAsync(tech, "LAP-0601");

        using var user = await SignedInAsync("user");
        var response = await user.GetAsync(
            new Uri($"{AssetsClient.Assets}/{asset.Id}/tickets", UriKind.Relative),
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Only the tickets linked to <em>this</em> asset, newest first — and the row carries
    /// the summary a panel draws without a second call per row.
    /// </summary>
    [Fact]
    public async Task It_returns_the_tickets_linked_to_that_asset_newest_first()
    {
        using var tech = await SignedInAsync("tech");
        var laptop = await AssetAsync(tech, "LAP-0602");
        var printer = await AssetAsync(tech, "PRN-0602");

        var first = await TicketAsync(tech, "Will not charge");
        var second = await TicketAsync(tech, "Screen flickers");
        var other = await TicketAsync(tech, "Paper jam");

        await TicketClient.LinksAssetAsync(tech, first.Id, laptop.Id, Token);
        await TicketClient.LinksAssetAsync(tech, second.Id, laptop.Id, Token);
        await TicketClient.LinksAssetAsync(tech, other.Id, printer.Id, Token);

        var page = await TicketsAsync(tech, laptop.Id);

        page.Total.ShouldBe(2);
        page.Items.Select(ticket => ticket.Number).ShouldBe([second.Number, first.Number]);

        var row = page.Items[0];
        row.Subject.ShouldBe("Screen flickers");
        row.RelatedAssetId.ShouldBe(laptop.Id);
        row.IsOpen.ShouldBeTrue();
        row.PriorityCode.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// An asset's history is the whole story of that machine — resolved and closed tickets
    /// included. Splitting by activity is a user page's question, not a machine's.
    /// </summary>
    [Fact]
    public async Task A_closed_ticket_stays_in_the_assets_history()
    {
        using var tech = await SignedInAsync("tech");
        var asset = await AssetAsync(tech, "LAP-0603");
        var ticket = await TicketAsync(tech, "Battery replaced");

        await TicketClient.LinksAssetAsync(tech, ticket.Id, asset.Id, Token);
        await WalkToClosedAsync(tech, ticket.Id);

        var page = await TicketsAsync(tech, asset.Id);

        var row = page.Items.ShouldHaveSingleItem();
        row.Status.ShouldBe(nameof(TicketStatus.Closed));
        row.IsOpen.ShouldBeFalse();
        row.ClosedAt.ShouldNotBeNull();
    }

    /// <summary>
    /// Unlinking takes the ticket off the asset's history — the correction of a mislinked
    /// ticket has to be visible on both sides.
    /// </summary>
    [Fact]
    public async Task Unlinking_removes_the_ticket_from_the_history()
    {
        using var tech = await SignedInAsync("tech");
        var asset = await AssetAsync(tech, "LAP-0604");
        var ticket = await TicketAsync(tech, "Mislinked");

        await TicketClient.LinksAssetAsync(tech, ticket.Id, asset.Id, Token);
        (await TicketsAsync(tech, asset.Id)).Total.ShouldBe(1);

        await TicketClient.LinksAssetAsync(tech, ticket.Id, null, Token);

        (await TicketsAsync(tech, asset.Id)).Total.ShouldBe(0);
    }

    [Fact]
    public async Task The_page_is_the_envelope_every_list_returns()
    {
        using var tech = await SignedInAsync("tech");
        var asset = await AssetAsync(tech, "LAP-0605");

        var first = await TicketAsync(tech, "One");
        var second = await TicketAsync(tech, "Two");

        await TicketClient.LinksAssetAsync(tech, first.Id, asset.Id, Token);
        await TicketClient.LinksAssetAsync(tech, second.Id, asset.Id, Token);

        var page = await TicketsAsync(tech, asset.Id, "page=2&pageSize=1");

        page.Total.ShouldBe(2);
        page.Page.ShouldBe(2);
        page.PageSize.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Number.ShouldBe(first.Number);
    }

    private static Task<PageDto<TicketSummaryDto>> TicketsAsync(HttpClient client, Guid assetId, string query = "") =>
        ApiClient.ListAsync<TicketSummaryDto>(
            client,
            query.Length == 0
                ? $"{AssetsClient.Assets}/{assetId}/tickets"
                : $"{AssetsClient.Assets}/{assetId}/tickets?{query}",
            Token);

    private static async Task<AssetDto> AssetAsync(HttpClient client, string tag)
    {
        var typeId = await AssetsClient.AnyTypeIdAsync(client, Token);
        return await AssetsClient.CreateAssetAsync(client, tag, typeId, Token);
    }

    private static async Task WalkToClosedAsync(HttpClient client, Guid ticketId)
    {
        var technicianId = (await AuthClient.ReadUserAsync(await AuthClient.MeAsync(client, Token), Token)).Id;

        await TicketClient.AssignsAsync(client, ticketId, technicianId, Token);
        (await TicketClient.ChangeStatusAsync(client, ticketId, TicketStatus.InProgress, Token))
            .EnsureSuccessStatusCode();
        (await TicketClient.ChangeStatusAsync(client, ticketId, TicketStatus.Resolved, Token, "Replaced the battery."))
            .EnsureSuccessStatusCode();
        (await TicketClient.ChangeStatusAsync(client, ticketId, TicketStatus.Closed, Token))
            .EnsureSuccessStatusCode();
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

    private async Task<TicketDetailDto> TicketAsync(HttpClient client, string subject)
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await DepartmentAsync();

        return await TicketClient.CreateAsync(client, reference, departmentId, subject, Token);
    }

    private Task<HttpClient> SignedInAsync(string userName) =>
        AuthClient.SignedInAsync(fixture, userName, Token);
}
