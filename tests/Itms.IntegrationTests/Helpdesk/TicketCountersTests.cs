using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// The queue's counters (WP-1.12).
/// </summary>
/// <remarks>
/// <para>
/// The central assertion of this class is not that a number is right in isolation — it is
/// that <b>every counter equals the total of the list its tile links to</b>. A KPI saying
/// six that opens a screen showing five is worse than no KPI, and it is the failure a
/// second hand-written set of predicates would eventually produce. The handler runs every
/// count through <c>ListTicketsHandler.Filter</c> for that reason; these tests are what
/// notice if it stops.
/// </para>
/// <para>
/// The other thing proved here is scope. A count is a disclosure: "there are four open
/// tickets" is information about tickets a User may not read.
/// </para>
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketCountersTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The four statuses the "Open" tile counts, as the client also spells them.</summary>
    private const string OpenQuery = "status=New&status=Assigned&status=InProgress&status=Waiting";

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_empty_queue_counts_zero_rather_than_failing()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var counters = await TicketClient.CountersAsync(admin, Token);

        counters.All.ShouldBe(0);
        counters.Open.ShouldBe(0);
        counters.Unassigned.ShouldBe(0);
        counters.Overdue.ShouldBe(0);
        counters.DueToday.ShouldBe(0);
        counters.Mine.ShouldBe(0);
    }

    [Fact]
    public async Task Every_counter_equals_the_total_of_the_list_it_links_to()
    {
        var world = await WorldAsync();

        var counters = await TicketClient.CountersAsync(world.Admin, Token);

        (await Total(world.Admin, string.Empty)).ShouldBe(counters.All);
        (await Total(world.Admin, OpenQuery)).ShouldBe(counters.Open);
        (await Total(world.Admin, $"{OpenQuery}&unassigned=true")).ShouldBe(counters.Unassigned);
        (await Total(world.Admin, "slaState=Breached")).ShouldBe(counters.Overdue);
    }

    [Fact]
    public async Task The_open_count_leaves_out_what_nobody_is_working()
    {
        var world = await WorldAsync();

        // Four tickets exist; one has been resolved and one cancelled.
        await Resolve(world.Admin, world.Resolved.Id);
        await Cancel(world.Admin, world.Cancelled.Id);

        var counters = await TicketClient.CountersAsync(world.Admin, Token);

        counters.All.ShouldBe(4);
        counters.Open.ShouldBe(2);
        (await Total(world.Admin, OpenQuery)).ShouldBe(2);
    }

    [Fact]
    public async Task The_unassigned_count_ignores_tickets_that_have_left_the_queue()
    {
        // A cancelled ticket has no assignee either, and counting it as work waiting to be
        // picked up would put a number on the tile that nobody can act on.
        var world = await WorldAsync();
        await Cancel(world.Admin, world.Cancelled.Id);

        var counters = await TicketClient.CountersAsync(world.Admin, Token);

        counters.Unassigned.ShouldBe(3);
        (await Total(world.Admin, $"{OpenQuery}&unassigned=true")).ShouldBe(3);
    }

    [Fact]
    public async Task Due_today_counts_against_the_day_the_caller_named()
    {
        var world = await WorldAsync();

        // Every seeded ticket is due within hours of creation, so a day-end far in the
        // past catches none and one far ahead catches every open one.
        var none = await TicketClient.CountersAsync(
            world.Admin, Token, dueBefore: DateTimeOffset.UtcNow.AddDays(-1));
        var all = await TicketClient.CountersAsync(
            world.Admin, Token, dueBefore: DateTimeOffset.UtcNow.AddYears(1));

        none.DueToday.ShouldBe(0);
        all.DueToday.ShouldBe(4);
    }

    [Fact]
    public async Task Due_today_leaves_out_a_ticket_that_has_already_been_resolved()
    {
        var world = await WorldAsync();
        await Resolve(world.Admin, world.Resolved.Id);

        var counters = await TicketClient.CountersAsync(
            world.Admin, Token, dueBefore: DateTimeOffset.UtcNow.AddYears(1));

        counters.DueToday.ShouldBe(3);
    }

    [Fact]
    public async Task Due_today_agrees_with_the_list_the_tile_opens()
    {
        var world = await WorldAsync();
        var dayEnd = DateTimeOffset.UtcNow.AddYears(1);

        var counters = await TicketClient.CountersAsync(world.Admin, Token, dueBefore: dayEnd);
        var listed = await Total(world.Admin, $"dueBefore={Encode(dayEnd)}");

        listed.ShouldBe(counters.DueToday);
    }

    [Fact]
    public async Task Mine_counts_what_a_technician_holds_rather_than_what_they_raised()
    {
        var world = await WorldAsync();
        using var tech = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);

        var before = await TicketClient.CountersAsync(tech, Token);
        (await TicketClient.AssignAsync(world.Admin, world.First.Id, techId, Token))
            .EnsureSuccessStatusCode();
        var after = await TicketClient.CountersAsync(tech, Token);

        before.Mine.ShouldBe(0);
        after.Mine.ShouldBe(1);
    }

    [Fact]
    public async Task Mine_counts_what_an_end_user_raised_rather_than_what_they_hold()
    {
        // The role-sensitive rule WP-1.9's "My tickets" chip follows, so the chip and its
        // count cannot mean two different things.
        var world = await WorldAsync();
        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var counters = await TicketClient.CountersAsync(endUser, Token);

        counters.Mine.ShouldBe(1);
    }

    [Fact]
    public async Task A_users_counters_describe_only_their_own_tickets()
    {
        var world = await WorldAsync();
        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var counters = await TicketClient.CountersAsync(endUser, Token);

        // Four tickets exist and one was raised for them. A count is a disclosure like any
        // other read, so the other three must not be visible even as a number.
        counters.All.ShouldBe(1);
        counters.Open.ShouldBe(1);
    }

    [Fact]
    public async Task The_counters_ignore_whatever_the_caller_last_filtered_by()
    {
        // Scope-wide by decision: a counter that moved with the filters would be describing
        // the filter rather than the queue.
        var world = await WorldAsync();

        var filtered = await TicketClient.ListAsync(world.Admin, "search=First", Token);
        var counters = await TicketClient.CountersAsync(world.Admin, Token);

        filtered.Total.ShouldBe(1);
        counters.All.ShouldBe(4);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_count_the_queue()
    {
        using var anonymous = fixture.CreateClient();

        var response = await anonymous.GetAsync(
            new Uri($"{TicketClient.Tickets}/counters", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
    }

    private static string Encode(DateTimeOffset value) =>
        Uri.EscapeDataString(value.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

    private static async Task<int> Total(HttpClient client, string query) =>
        (await TicketClient.ListAsync(client, query, Token)).Total;

    private async Task Resolve(HttpClient admin, Guid ticketId)
    {
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);
        (await TicketClient.AssignAsync(admin, ticketId, techId, Token)).EnsureSuccessStatusCode();
        (await TicketClient.ChangeStatusAsync(admin, ticketId, TicketStatus.InProgress, Token))
            .EnsureSuccessStatusCode();
        (await TicketClient.ChangeStatusAsync(
            admin, ticketId, TicketStatus.Resolved, Token, resolutionNotes: "Done."))
            .EnsureSuccessStatusCode();
    }

    private static async Task Cancel(HttpClient admin, Guid ticketId) =>
        (await TicketClient.ChangeStatusAsync(admin, ticketId, TicketStatus.Cancelled, Token))
            .EnsureSuccessStatusCode();

    /// <summary>Four tickets, one of them raised for the end user.</summary>
    private async Task<World> WorldAsync()
    {
        var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);

        var water = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var userId = await TicketClient.UserIdAsync(fixture, "user", Token);

        var first = await TicketClient.CreateAsync(admin, reference, water, "First", Token);
        var resolved = await TicketClient.CreateAsync(admin, reference, water, "Second", Token);
        var cancelled = await TicketClient.CreateAsync(admin, reference, water, "Third", Token);
        await TicketClient.CreateAsync(
            admin, reference, water, "Fourth", Token, requesterId: userId);

        return new World(admin, first, resolved, cancelled);
    }

    private sealed record World(
        HttpClient Admin,
        TicketDetailDto First,
        TicketDetailDto Resolved,
        TicketDetailDto Cancelled);
}
