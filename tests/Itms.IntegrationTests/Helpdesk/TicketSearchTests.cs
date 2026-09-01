using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// The queue's free-text search (WP-1.12).
/// </summary>
/// <remarks>
/// Two things are being proved here and only one of them is about finding tickets. The
/// other is that <c>TicketScope</c> still comes first: a search is a filter, filters run
/// after the scope, and a search that ran before it would let any account find any
/// ticket by typing part of its subject. That is the whole reason this package is marked
/// sensitive.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketSearchTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Searching_matches_the_subject()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, "search=printer", Token);

        page.Items.Select(t => t.Subject).ShouldBe(["Printer jammed again"]);
    }

    [Fact]
    public async Task Searching_matches_the_ticket_number()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, $"search={world.Printer.Number}", Token);

        page.Items.Select(t => t.Id).ShouldBe([world.Printer.Id]);
    }

    [Fact]
    public async Task Searching_matches_the_requester_the_ticket_cached()
    {
        // The cached name, not a live lookup (§3 rule 6) — so somebody is found by the name
        // the queue is actually displaying beside their ticket.
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, "search=Uma", Token);

        page.Items.Select(t => t.Subject).ShouldBe(["Raised for somebody else"]);
    }

    [Fact]
    public async Task Searching_ignores_case()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, "search=PRINTER", Token);

        page.Total.ShouldBe(1);
    }

    [Fact]
    public async Task Searching_matches_a_fragment_from_the_middle_of_a_subject()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, "search=jammed", Token);

        page.Total.ShouldBe(1);
    }

    [Fact]
    public async Task A_search_that_matches_nothing_is_an_empty_page_and_not_a_404()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, "search=nothingmatchesthis", Token);

        page.Items.ShouldBeEmpty();
        page.Total.ShouldBe(0);
    }

    [Fact]
    public async Task A_blank_search_is_no_filter_at_all()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, "search=%20%20", Token);

        page.Total.ShouldBe(3);
    }

    [Fact]
    public async Task A_wildcard_is_matched_literally_rather_than_expanded()
    {
        // An unescaped % would match every ticket. This is the assertion that says the
        // shared kernel's escaping is actually reaching the query.
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, "search=%25", Token);

        page.Total.ShouldBe(0);
    }

    [Fact]
    public async Task An_underscore_is_matched_literally_too()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, "search=_", Token);

        page.Total.ShouldBe(0);
    }

    [Fact]
    public async Task Searching_composes_with_the_other_filters_rather_than_replacing_them()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(
            world.Admin, $"search=e&departmentId={world.PowerDepartmentId}", Token);

        page.Items.Select(t => t.Subject).ShouldBe(["Power feed unstable"]);
    }

    [Fact]
    public async Task A_user_searching_never_finds_a_ticket_they_did_not_raise()
    {
        // The one that matters. "Printer jammed again" exists, matches the term exactly,
        // and belongs to somebody else — the scope has to win.
        var world = await WorldAsync();
        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var page = await TicketClient.ListAsync(endUser, "search=printer", Token);

        page.Items.ShouldBeEmpty();
        page.Total.ShouldBe(0);
    }

    [Fact]
    public async Task A_user_searching_still_finds_their_own()
    {
        var world = await WorldAsync();
        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var page = await TicketClient.ListAsync(endUser, "search=somebody", Token);

        page.Items.Select(t => t.Id).ShouldBe([world.ForTheUser.Id]);
    }

    /// <summary>
    /// Three tickets: one whose subject and number are the search target, one raised for
    /// the end user, and one in another department — so every assertion has something it
    /// must exclude as well as something it must return.
    /// </summary>
    private async Task<World> WorldAsync()
    {
        var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);

        var water = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var power = await TicketClient.DepartmentAsync(admin, "Power Generation", Token);
        var userId = await TicketClient.UserIdAsync(fixture, "user", Token);

        var printer = await TicketClient.CreateAsync(admin, reference, water, "Printer jammed again", Token);
        var forTheUser = await TicketClient.CreateAsync(
            admin, reference, water, "Raised for somebody else", Token, requesterId: userId);
        await TicketClient.CreateAsync(admin, reference, power, "Power feed unstable", Token);

        return new World(admin, power, printer, forTheUser);
    }

    private sealed record World(
        HttpClient Admin,
        Guid PowerDepartmentId,
        TicketDetailDto Printer,
        TicketDetailDto ForTheUser);
}
