using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;
using Npgsql;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// The SLA over the wire: what a created ticket is promised, what Waiting does to it, what
/// stops the response clock, and what the queue's <c>slaState</c> filter answers.
/// </summary>
/// <remarks>
/// <para>
/// <b>The clock is the host's real one here, and that is the point.</b> The arithmetic is
/// walked to the tick in the unit suite against a <c>FakeClock</c>; what these tests prove
/// is the plumbing around it — that the columns are written, that the projections carry
/// them, and that the filter and the response agree. A test that needed a ticket to be
/// hours overdue moves the ticket's deadline rather than the clock, because the fixture
/// boots one host for the whole assembly and a clock one test wound forward would be a
/// clock every other test inherited.
/// </para>
/// <para>
/// <b>The filter test is the drift guard between the two implementations of the SLA
/// rule</b> — <c>SlaAssessment</c> in memory and <c>TicketSlaFilter</c> in SQL. Every row
/// the filter returns for a state must also describe itself as being in that state, and
/// the five states between them must account for every ticket. A change made to one rule
/// and not the other fails here.
/// </para>
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketSlaEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    /// <summary>
    /// The clients this test opened, closed when it ends. Every test here arranges its own,
    /// and a browser left open is a cookie jar left open.
    /// </summary>
    private readonly List<HttpClient> _clients = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _clients.Clear();

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task A_created_ticket_carries_both_clocks_from_its_creation_instant()
    {
        var arranged = await ArrangeAsync();
        var ticket = arranged.Ticket;
        var targets = arranged.Reference.Targets;

        ticket.Sla.ResponseTargetMinutes.ShouldBe(targets.ResponseMinutes);
        ticket.Sla.ResolutionTargetMinutes.ShouldBe(targets.ResolutionMinutes);
        ticket.Sla.ResponseDueAt.ShouldBe(ticket.CreatedAt.AddMinutes(targets.ResponseMinutes));
        ticket.Sla.ResolutionDueAt.ShouldBe(ticket.CreatedAt.AddMinutes(targets.ResolutionMinutes));
        ticket.Sla.ResponseWarnAt.ShouldBe(ticket.CreatedAt.AddSeconds(targets.ResponseMinutes * 48));
        ticket.Sla.ResolutionWarnAt.ShouldBe(ticket.CreatedAt.AddSeconds(targets.ResolutionMinutes * 48));

        ticket.DueAt.ShouldBe(ticket.Sla.ResolutionDueAt);
        ticket.Sla.RespondedAt.ShouldBeNull();
        ticket.Sla.PausedAt.ShouldBeNull();
        ticket.Sla.PausedSeconds.ShouldBe(0);
        ticket.Sla.ResponseState.ShouldBe(SlaState.Pending);
        ticket.Sla.ResolutionState.ShouldBe(SlaState.Pending);
        ticket.Sla.IsPaused.ShouldBeFalse();
    }

    /// <summary>
    /// The create response is read back through the detail projection, so the two cannot
    /// disagree — which is exactly why it is worth asserting once.
    /// </summary>
    [Fact]
    public async Task The_detail_read_and_the_create_response_describe_the_same_clocks()
    {
        var arranged = await ArrangeAsync();

        var (read, _) = await TicketClient.GetAsync(arranged.Tech, arranged.Ticket.Id, Token);

        read.Sla.ResponseDueAt.ShouldBe(arranged.Ticket.Sla.ResponseDueAt);
        read.Sla.ResolutionDueAt.ShouldBe(arranged.Ticket.Sla.ResolutionDueAt);
        read.Sla.ResolutionWarnAt.ShouldBe(arranged.Ticket.Sla.ResolutionWarnAt);
        read.DueAt.ShouldBe(arranged.Ticket.DueAt);
    }

    [Fact]
    public async Task Parking_a_ticket_in_Waiting_stops_its_resolution_clock()
    {
        var arranged = await ArrangeAsync();
        var ticket = await InProgressAsync(arranged);

        await MoveAsync(arranged, ticket.Id, TicketStatus.Waiting);

        var (waiting, _) = await TicketClient.GetAsync(arranged.Tech, ticket.Id, Token);

        waiting.Sla.IsPaused.ShouldBeTrue();
        waiting.Sla.PausedAt.ShouldNotBeNull();
        waiting.Sla.ResolutionDueAt.ShouldBe(ticket.Sla.ResolutionDueAt);
    }

    [Fact]
    public async Task Resuming_pushes_the_deadline_out_by_the_time_spent_waiting()
    {
        var arranged = await ArrangeAsync();
        var ticket = await InProgressAsync(arranged);

        await MoveAsync(arranged, ticket.Id, TicketStatus.Waiting);

        // The host's clock cannot be wound forward here, so the pause is made real in the
        // column instead: the ticket has been parked for two hours as far as the resume
        // arithmetic is concerned.
        await ExecuteAsync(
            $"UPDATE helpdesk.tickets SET sla_paused_at = sla_paused_at - interval '2 hours' WHERE id = '{ticket.Id}'");

        await MoveAsync(arranged, ticket.Id, TicketStatus.InProgress);

        var (resumed, _) = await TicketClient.GetAsync(arranged.Tech, ticket.Id, Token);

        resumed.Sla.IsPaused.ShouldBeFalse();
        resumed.Sla.PausedAt.ShouldBeNull();
        resumed.Sla.PausedSeconds.ShouldBeGreaterThanOrEqualTo(7200);
        resumed.Sla.ResolutionDueAt.ShouldBeGreaterThan(ticket.Sla.ResolutionDueAt.AddHours(2).AddSeconds(-1));
        resumed.DueAt.ShouldBe(resumed.Sla.ResolutionDueAt);

        // The response clock is not moved by a pause: SPEC.md §2 pauses the resolution
        // clock and names nothing else.
        resumed.Sla.ResponseDueAt.ShouldBe(ticket.Sla.ResponseDueAt);
    }

    [Fact]
    public async Task A_public_comment_from_a_technician_stops_the_response_clock()
    {
        var arranged = await ArrangeAsync();

        var comment = await TicketThreadClient.CommentsAsync(
            arranged.Tech, arranged.Ticket.Id, "Looking at it now.", Token);

        var (answered, _) = await TicketClient.GetAsync(arranged.Tech, arranged.Ticket.Id, Token);

        // To the millisecond, not to the tick: the comment's instant comes back from the
        // post as .NET wrote it, and the stamp comes back through PostgreSQL, whose
        // timestamps are microseconds. They are the same instant, rounded differently.
        answered.Sla.RespondedAt.ShouldNotBeNull();
        answered.Sla.RespondedAt.Value.ShouldBe(comment.CreatedAt, TimeSpan.FromMilliseconds(1));
        answered.Sla.ResponseState.ShouldBe(SlaState.Met);
    }

    /// <summary>
    /// An internal note is not a response: the requester cannot see it, so nobody has
    /// answered them.
    /// </summary>
    [Fact]
    public async Task An_internal_note_does_not_stop_the_response_clock()
    {
        var arranged = await ArrangeAsync();

        await TicketThreadClient.CommentsAsync(
            arranged.Tech, arranged.Ticket.Id, "Charger stock is out.", Token, isInternal: true);

        var (unanswered, _) = await TicketClient.GetAsync(arranged.Tech, arranged.Ticket.Id, Token);

        unanswered.Sla.RespondedAt.ShouldBeNull();
        unanswered.Sla.ResponseState.ShouldBe(SlaState.Pending);
    }

    /// <summary>
    /// Nor is the requester's own follow-up: a ticket nobody has replied to does not stop
    /// owing a reply because the person waiting added to it.
    /// </summary>
    [Fact]
    public async Task A_requesters_own_comment_does_not_stop_the_response_clock()
    {
        var arranged = await ArrangeAsync();
        var user = await SignedInAsync("user");

        await TicketThreadClient.CommentsAsync(
            user, arranged.Ticket.Id, "It is still not working.", Token);

        var (unanswered, _) = await TicketClient.GetAsync(arranged.Tech, arranged.Ticket.Id, Token);

        unanswered.Sla.RespondedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Resolving_a_ticket_nobody_replied_to_stops_both_clocks()
    {
        var arranged = await ArrangeAsync();
        var ticket = await InProgressAsync(arranged);

        await MoveAsync(arranged, ticket.Id, TicketStatus.Resolved, "Replaced the charger.");

        var (resolved, _) = await TicketClient.GetAsync(arranged.Tech, ticket.Id, Token);

        resolved.Sla.RespondedAt.ShouldBe(resolved.ResolvedAt);
        resolved.Sla.ResolvedAt.ShouldBe(resolved.ResolvedAt);
        resolved.Sla.ResponseState.ShouldBe(SlaState.Met);
        resolved.Sla.ResolutionState.ShouldBe(SlaState.Met);
    }

    [Fact]
    public async Task A_cancelled_ticket_reports_no_outcome_however_overdue_it_was()
    {
        var arranged = await ArrangeAsync();
        var ticket = arranged.Ticket;

        await OverdueAsync(ticket.Id);
        await MoveAsync(arranged, ticket.Id, TicketStatus.Cancelled);

        var (cancelled, _) = await TicketClient.GetAsync(arranged.Tech, ticket.Id, Token);

        cancelled.Sla.ResolutionState.ShouldBe(SlaState.Stopped);
        cancelled.Sla.ResponseState.ShouldBe(SlaState.Stopped);
    }

    /// <summary>
    /// The SLA is not internal. A requester reading their own ticket sees the same clocks
    /// the technician does — there is nothing in them the internal-note rule protects.
    /// </summary>
    [Fact]
    public async Task A_requester_sees_the_same_clocks_on_their_own_ticket()
    {
        var arranged = await ArrangeAsync();
        var user = await SignedInAsync("user");

        var (theirs, _) = await TicketClient.GetAsync(user, arranged.Ticket.Id, Token);

        theirs.Sla.ResolutionDueAt.ShouldBe(arranged.Ticket.Sla.ResolutionDueAt);
        theirs.Sla.ResolutionState.ShouldBe(arranged.Ticket.Sla.ResolutionState);
    }

    [Fact]
    public async Task The_queue_carries_the_clocks_on_every_row()
    {
        var arranged = await ArrangeAsync();

        var page = await TicketClient.ListAsync(arranged.Tech, string.Empty, Token);

        var row = page.Items.ShouldHaveSingleItem();

        row.Sla.ResolutionDueAt.ShouldBe(arranged.Ticket.Sla.ResolutionDueAt);
        row.DueAt.ShouldBe(arranged.Ticket.DueAt);
        row.Sla.ResolutionState.ShouldBe(SlaState.Pending);
    }

    /// <summary>
    /// The filter, against a queue holding one ticket in each state — and the drift guard
    /// between the SQL rule and the in-memory one.
    /// </summary>
    [Fact]
    public async Task The_sla_state_filter_partitions_the_queue()
    {
        var arranged = await ArrangeAsync();
        var expected = await AQueueInEveryStateAsync(arranged);

        var seen = new List<Guid>();

        foreach (var state in Enum.GetValues<SlaState>())
        {
            var page = await TicketClient.ListAsync(arranged.Tech, $"slaState={state}", Token);

            // Every row the SQL filter chose describes itself, through the in-memory rule,
            // as being in exactly the state that was asked for. This is the guard.
            page.Items.ShouldAllBe(row => row.Sla.ResolutionState == state);

            page.Items.Select(row => row.Id).OrderBy(id => id)
                .ShouldBe(expected[state].OrderBy(id => id), $"the {state} view");

            seen.AddRange(page.Items.Select(row => row.Id));
        }

        // No ticket is in two states, and none is in none.
        var everything = await TicketClient.ListAsync(arranged.Tech, "pageSize=200", Token);

        seen.Order().ShouldBe(everything.Items.Select(row => row.Id).Order());
    }

    [Fact]
    public async Task The_overdue_view_excludes_a_ticket_that_is_merely_parked()
    {
        var arranged = await ArrangeAsync();
        var ticket = await InProgressAsync(arranged);

        // Overdue in wall-clock terms, but the clock stopped before the deadline passed:
        // the pause is what the filter has to honour.
        await MoveAsync(arranged, ticket.Id, TicketStatus.Waiting);
        await ExecuteAsync(
            $"""
             UPDATE helpdesk.tickets
             SET due_at = now() - interval '1 hour',
                 sla_resolution_warn_at = now() - interval '2 hours',
                 sla_paused_at = now() - interval '3 hours'
             WHERE id = '{ticket.Id}'
             """);

        var breached = await TicketClient.ListAsync(arranged.Tech, $"slaState={SlaState.Breached}", Token);

        breached.Items.ShouldBeEmpty();

        var pending = await TicketClient.ListAsync(arranged.Tech, $"slaState={SlaState.Pending}", Token);

        pending.Items.Select(row => row.Id).ShouldContain(ticket.Id);
    }

    /// <summary>
    /// One ticket in each of the five states, built through the endpoints where a
    /// transition can produce the state and through the column where only the passage of
    /// time could.
    /// </summary>
    private async Task<Dictionary<SlaState, List<Guid>>> AQueueInEveryStateAsync(Arranged arranged)
    {
        // The ticket ArrangeAsync already raised: nothing has happened to it.
        var pending = arranged.Ticket.Id;

        var approaching = (await NewTicketAsync(arranged, "Printer jams on duplex")).Id;
        await ExecuteAsync(
            $"""
             UPDATE helpdesk.tickets
             SET sla_resolution_warn_at = now() - interval '1 minute'
             WHERE id = '{approaching}'
             """);

        var breached = (await NewTicketAsync(arranged, "VPN drops every ten minutes")).Id;
        await OverdueAsync(breached);

        // Resolved well inside its four-hour target, which is what Met means.
        var met = await NewTicketAsync(arranged, "Password reset");
        await ResolveAsync(arranged, met.Id);

        var stopped = (await NewTicketAsync(arranged, "Raised in error")).Id;
        await MoveAsync(arranged, stopped, TicketStatus.Cancelled);

        return new Dictionary<SlaState, List<Guid>>
        {
            [SlaState.Pending] = [pending],
            [SlaState.Approaching] = [approaching],
            [SlaState.Breached] = [breached],
            [SlaState.Met] = [met.Id],
            [SlaState.Stopped] = [stopped],
        };
    }

    /// <summary>Walks a ticket from New to Resolved through the real endpoints.</summary>
    private async Task ResolveAsync(Arranged arranged, Guid ticketId)
    {
        var technicianId = await TicketClient.UserIdAsync(fixture, "tech", Token);

        await TicketClient.AssignsAsync(arranged.Tech, ticketId, technicianId, Token);
        await MoveAsync(arranged, ticketId, TicketStatus.InProgress);
        await MoveAsync(arranged, ticketId, TicketStatus.Resolved, "Reset and confirmed with the requester.");
    }

    /// <summary>Moves a ticket's deadline into the past, which is the one thing the clock cannot be asked to do here.</summary>
    private Task OverdueAsync(Guid ticketId) =>
        ExecuteAsync(
            $"""
             UPDATE helpdesk.tickets
             SET due_at = now() - interval '1 hour',
                 sla_resolution_warn_at = now() - interval '2 hours'
             WHERE id = '{ticketId}'
             """);

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync(Token);
        await using var command = new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync(Token);
    }

    private static async Task<TicketDetailDto> NewTicketAsync(Arranged arranged, string subject) =>
        await TicketClient.CreateAsync(
            arranged.Tech, arranged.Reference, arranged.DepartmentId, subject, Token, arranged.RequesterId);

    private async Task<TicketDetailDto> InProgressAsync(Arranged arranged)
    {
        var technicianId = await TicketClient.UserIdAsync(fixture, "tech", Token);

        await TicketClient.AssignsAsync(arranged.Tech, arranged.Ticket.Id, technicianId, Token);
        await MoveAsync(arranged, arranged.Ticket.Id, TicketStatus.InProgress);

        var (ticket, _) = await TicketClient.GetAsync(arranged.Tech, arranged.Ticket.Id, Token);

        return ticket;
    }

    private static async Task MoveAsync(
        Arranged arranged,
        Guid ticketId,
        TicketStatus status,
        string? resolutionNotes = null)
    {
        // Waiting requires a hold reason since WP-1.15. None of these tests is about the
        // reason — they are about what parking does to the clock — so it is supplied here.
        var response = await TicketClient.ChangeStatusAsync(
            arranged.Tech,
            ticketId,
            status,
            Token,
            resolutionNotes,
            holdReason: status == TicketStatus.Waiting ? "Waiting on the vendor." : null);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// A technician, and one ticket raised on the seeded end user's behalf.
    /// </summary>
    /// <remarks>
    /// <b>The requester is the end user, not the technician who filed it</b>, because the
    /// response clock turns on which of the two is which: a technician's public comment on
    /// their own ticket is not a reply to anybody. A suite arranged the other way round
    /// would pass the response tests without ever exercising the rule.
    /// </remarks>
    /// <returns>The arranged world.</returns>
    private async Task<Arranged> ArrangeAsync()
    {
        var admin = await SignedInAsync("admin");
        var tech = await SignedInAsync("tech");

        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var requesterId = await TicketClient.UserIdAsync(fixture, "user", Token);

        var ticket = await TicketClient.CreateAsync(
            tech, reference, departmentId, "Laptop will not charge", Token, requesterId);

        return new Arranged(tech, reference, departmentId, requesterId, ticket);
    }

    /// <summary>A signed-in browser for one of the seeded development accounts.</summary>
    /// <param name="userName">The account: <c>admin</c>, <c>tech</c>, or <c>user</c>.</param>
    /// <returns>The client, closed when the test ends.</returns>
    private async Task<HttpClient> SignedInAsync(string userName)
    {
        var client = await AuthClient.SignedInAsync(fixture, userName, Token);
        _clients.Add(client);

        return client;
    }

    /// <summary>The signed-in technician, the reference data, and one raised ticket.</summary>
    /// <param name="Tech">A technician client, which every test here drives.</param>
    /// <param name="Reference">The seeded category and priority, with that priority's targets.</param>
    /// <param name="DepartmentId">The department the tickets are filed against.</param>
    /// <param name="RequesterId">The end user every ticket here is raised for.</param>
    /// <param name="Ticket">A ticket nothing has happened to yet.</param>
    private sealed record Arranged(
        HttpClient Tech,
        TicketWriter.ReferenceData Reference,
        Guid DepartmentId,
        Guid RequesterId,
        TicketDetailDto Ticket);
}
