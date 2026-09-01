using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// ARCHITECTURE.md §7's row-level rule, asserted at the API level.
/// </summary>
/// <remarks>
/// <para>
/// "A <b>User</b> may read and comment on tickets where they are the requester, and nothing
/// else." WP-1.5 is the package that first had a requester-scoped read to enforce that
/// against, so this is where it is proved. §7 also says the React app hiding a control is
/// never the enforcement — every request below is the hand-crafted one that skips the
/// interface entirely.
/// </para>
/// <para>
/// <b>WP-1.7 must extend this file, not just trust it.</b> These tests answer "which
/// tickets"; they say nothing about "which parts of one", and an internal note is exactly a
/// part a requester may not read. When comments land, the equivalent assertion is that a
/// User fetching their own ticket receives no internal note in the payload.
/// </para>
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketAccessTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_user_reading_the_queue_sees_only_their_own_tickets()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.User, string.Empty, Token);

        page.Total.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(world.TheirTicket.Id);
    }

    [Fact]
    public async Task A_technician_reading_the_queue_sees_every_ticket()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Tech, string.Empty, Token);

        page.Total.ShouldBe(2);
    }

    [Fact]
    public async Task A_user_may_read_their_own_ticket_in_full()
    {
        var world = await WorldAsync();

        var (ticket, etag) = await TicketClient.GetAsync(world.User, world.TheirTicket.Id, Token);

        ticket.Id.ShouldBe(world.TheirTicket.Id);
        etag.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// A 404 rather than a 403, deliberately: telling the two apart would let any account
    /// walk the id space and count the tickets it cannot see. ARCHITECTURE.md §6 allows
    /// exactly this exception, and this is it.
    /// </summary>
    [Fact]
    public async Task Somebody_elses_ticket_is_a_404_and_not_a_403()
    {
        var world = await WorldAsync();

        var response = await world.User.GetAsync(
            new Uri($"{TicketClient.Tickets}/{world.OtherTicket.Id}", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.ticket_not_found");
    }

    /// <summary>
    /// The answer for a ticket somebody else raised and for one that was never issued must
    /// be indistinguishable, or the 404 is a 403 wearing a disguise that leaks anyway.
    /// </summary>
    [Fact]
    public async Task Somebody_elses_ticket_is_indistinguishable_from_one_that_does_not_exist()
    {
        var world = await WorldAsync();

        var theirs = await world.User.GetAsync(
            new Uri($"{TicketClient.Tickets}/{world.OtherTicket.Id}", UriKind.Relative), Token);
        var nobodys = await world.User.GetAsync(
            new Uri($"{TicketClient.Tickets}/{Guid.CreateVersion7()}", UriKind.Relative), Token);

        theirs.StatusCode.ShouldBe(nobodys.StatusCode);

        var one = await ApiClient.ReadAsync<ProblemDto>(theirs, Token);
        var other = await ApiClient.ReadAsync<ProblemDto>(nobodys, Token);

        one.Code.ShouldBe(other.Code);
        one.Detail.ShouldBe(other.Detail);
    }

    /// <summary>The answer the human gave at the scope gate: a requester sees their own timeline.</summary>
    [Fact]
    public async Task A_user_may_read_their_own_tickets_timeline()
    {
        var world = await WorldAsync();

        await TicketWriter.ParkAsync(fixture.DataSource, world.TheirTicket.Id, TicketStatus.InProgress, Token);
        (await TicketClient.ChangeStatusAsync(world.Tech, world.TheirTicket.Id, TicketStatus.Waiting, Token, holdReason: "Waiting on the vendor."))
            .EnsureSuccessStatusCode();

        var page = await ApiClient.ListAsync<TicketHistoryDto>(
            world.User, $"{TicketClient.Tickets}/{world.TheirTicket.Id}/history", Token);

        // The status move and the hold reason, at one instant (WP-1.15). The requester
        // seeing why their own ticket is parked is the point of the reason existing.
        page.Total.ShouldBe(2);
        page.Items.Select(entry => entry.ToValue)
            .ShouldBe([nameof(TicketStatus.Waiting), "Waiting on the vendor."], ignoreOrder: true);
    }

    [Fact]
    public async Task A_user_cannot_read_somebody_elses_timeline()
    {
        var world = await WorldAsync();

        var response = await world.User.GetAsync(
            new Uri($"{TicketClient.Tickets}/{world.OtherTicket.Id}/history", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A filter cannot widen the scope: the narrowing is composed into the query before any
    /// filter is, so asking for somebody else's tickets by id returns nothing rather than
    /// theirs.
    /// </summary>
    [Fact]
    public async Task A_user_filtering_by_another_requester_gets_nothing_rather_than_their_queue()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.User, $"requesterId={world.TechId}", Token);

        page.Total.ShouldBe(0);
    }

    /// <summary>
    /// Transitions stay Technician-only, unchanged from WP-1.3: §7 gives a User reading and
    /// commenting "and nothing else", so they cannot cancel or close even their own ticket.
    /// </summary>
    [Fact]
    public async Task A_user_cannot_move_their_own_ticket()
    {
        var world = await WorldAsync();

        var response = await TicketClient.ChangeStatusAsync(
            world.User, world.TheirTicket.Id, TicketStatus.Cancelled, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var state = await TicketWriter.StateAsync(fixture.Services, world.TheirTicket.Id, Token);
        state.Status.ShouldBe(TicketStatus.New);
    }

    private async Task<World> WorldAsync()
    {
        var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var tech = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var user = await AuthClient.SignedInAsync(fixture, "user", Token);

        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var userId = await TicketClient.UserIdAsync(fixture, "user", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);

        var theirs = await TicketClient.CreateAsync(
            admin, reference, departmentId, "The user's own", Token, requesterId: userId);
        var others = await TicketClient.CreateAsync(
            admin, reference, departmentId, "Somebody else's", Token, requesterId: techId);

        admin.Dispose();

        return new World(tech, user, techId, theirs, others);
    }

    private sealed record World(
        HttpClient Tech,
        HttpClient User,
        Guid TechId,
        TicketDetailDto TheirTicket,
        TicketDetailDto OtherTicket);
}
