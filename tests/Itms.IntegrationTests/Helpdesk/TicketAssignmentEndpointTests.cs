using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.AuditModule;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// <c>POST /api/v1/tickets/{id}/assignments</c> — WP-1.6 over the wire.
/// </summary>
/// <remarks>
/// <para>
/// One route carries all three operations, because assigning, reassigning, and unassigning
/// are the same fact about a ticket. What separates them is only what the status does:
/// the first assignment moves <c>New → Assigned</c>, an unassignment moves it back, and a
/// reassignment moves nothing.
/// </para>
/// <para>
/// <b>The eligibility rules are what this suite is really for.</b> Whether an account
/// exists, is active, and holds a role a ticket may be given to are facts the entity
/// cannot see, so they are only true if the handler reads them through
/// <c>IUserLookup</c> — and only proved by asserting them against real seeded accounts.
/// ARCHITECTURE.md §7 is explicit that a picker hiding the wrong names is never the
/// enforcement.
/// </para>
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketAssignmentEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// WP-1.6's first criterion: assigning moves New → Assigned automatically, in the same
    /// call that names the technician.
    /// </summary>
    [Fact]
    public async Task Assigning_a_new_ticket_moves_it_to_Assigned_and_names_the_technician()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var ticketId = await NewTicketAsync(technician);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);

        var (assignment, _) = await TicketClient.AssignsAsync(technician, ticketId, techId, Token);

        assignment.PreviousStatus.ShouldBe(TicketStatus.New);
        assignment.Status.ShouldBe(TicketStatus.Assigned);
        assignment.PreviousAssigneeId.ShouldBeNull();
        assignment.PreviousAssigneeName.ShouldBeNull();
        assignment.AssigneeId.ShouldBe(techId);
        assignment.AssigneeName.ShouldNotBeNullOrWhiteSpace();

        // The destinations come off the state machine, so a screen never restates the
        // table — and from Assigned they now include New, which is the unassignment.
        assignment.AllowedNextStatuses.ShouldContain(TicketStatus.InProgress);

        var (ticket, _) = await TicketClient.GetAsync(technician, ticketId, Token);
        ticket.Status.ShouldBe(TicketStatus.Assigned);
        ticket.AssigneeId.ShouldBe(techId);
        ticket.AssigneeName.ShouldBe(assignment.AssigneeName);
    }

    /// <summary>
    /// WP-1.6's second criterion: reassigning an in-progress ticket preserves its status.
    /// </summary>
    [Fact]
    public async Task Reassigning_an_in_progress_ticket_preserves_its_status()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);
        var adminId = await TicketClient.UserIdAsync(fixture, "admin", Token);

        var ticketId = await NewTicketAsync(technician);
        await TicketClient.AssignsAsync(technician, ticketId, techId, Token);
        (await TicketClient.ChangeStatusAsync(technician, ticketId, TicketStatus.InProgress, Token))
            .EnsureSuccessStatusCode();

        var (assignment, _) = await TicketClient.AssignsAsync(technician, ticketId, adminId, Token);

        assignment.PreviousStatus.ShouldBe(TicketStatus.InProgress);
        assignment.Status.ShouldBe(TicketStatus.InProgress);
        assignment.PreviousAssigneeId.ShouldBe(techId);
        assignment.AssigneeId.ShouldBe(adminId);

        (await TicketWriter.StateAsync(fixture.Services, ticketId, Token)).Status.ShouldBe(TicketStatus.InProgress);
    }

    /// <summary>
    /// Unassigning returns the ticket to New rather than leaving it Assigned to nobody —
    /// the state the <c>Assigned → New</c> edge was added to make unreachable.
    /// </summary>
    [Fact]
    public async Task Unassigning_returns_the_ticket_to_New_and_clears_the_holder()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);
        var ticketId = await AssignedTicketAsync(technician, techId);

        var (assignment, _) = await TicketClient.AssignsAsync(technician, ticketId, assigneeId: null, Token);

        assignment.PreviousStatus.ShouldBe(TicketStatus.Assigned);
        assignment.Status.ShouldBe(TicketStatus.New);
        assignment.PreviousAssigneeId.ShouldBe(techId);
        assignment.AssigneeId.ShouldBeNull();
        assignment.AssigneeName.ShouldBeNull();

        var (ticket, _) = await TicketClient.GetAsync(technician, ticketId, Token);
        ticket.Status.ShouldBe(TicketStatus.New);
        ticket.AssigneeId.ShouldBeNull();
    }

    /// <summary>
    /// A ticket somebody has started working belongs to them until it is handed on.
    /// Unassigning it would leave an In Progress ticket with no owner.
    /// </summary>
    [Fact]
    public async Task A_ticket_past_Assigned_cannot_be_unassigned()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);
        var ticketId = await AssignedTicketAsync(technician, techId);
        (await TicketClient.ChangeStatusAsync(technician, ticketId, TicketStatus.InProgress, Token))
            .EnsureSuccessStatusCode();

        var response = await TicketClient.AssignAsync(technician, ticketId, assigneeId: null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.cannot_unassign");
        (await TicketWriter.StateAsync(fixture.Services, ticketId, Token)).Status.ShouldBe(TicketStatus.InProgress);
    }

    /// <summary>
    /// <b>The rule the contract was widened for.</b> An end user is a real, active account
    /// and is still not somebody a ticket can be given to — checked server-side against the
    /// roles <c>IUserLookup</c> now carries, because §7 says hiding the name in a picker is
    /// never the enforcement.
    /// </summary>
    [Fact]
    public async Task A_ticket_cannot_be_assigned_to_an_end_user()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var endUserId = await TicketClient.UserIdAsync(fixture, "user", Token);
        var ticketId = await NewTicketAsync(technician);

        var response = await TicketClient.AssignAsync(technician, ticketId, endUserId, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.assignee_not_technician");
        problem.Errors.ShouldNotBeNull().ShouldContainKey("assigneeId");

        var state = await TicketWriter.StateAsync(fixture.Services, ticketId, Token);
        state.Status.ShouldBe(TicketStatus.New);
    }

    /// <summary>
    /// An administrator working the queue is somebody the queue can be given to, matching
    /// <c>TicketScope.SeesEveryTicket</c>.
    /// </summary>
    [Fact]
    public async Task A_ticket_can_be_assigned_to_an_administrator()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var adminId = await TicketClient.UserIdAsync(fixture, "admin", Token);
        var ticketId = await NewTicketAsync(technician);

        var (assignment, _) = await TicketClient.AssignsAsync(technician, ticketId, adminId, Token);

        assignment.AssigneeId.ShouldBe(adminId);
        assignment.Status.ShouldBe(TicketStatus.Assigned);
    }

    /// <summary>An id Identity does not know is a field-level 400, not a 404 on the ticket.</summary>
    [Fact]
    public async Task An_unknown_assignee_is_refused_against_the_field()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var ticketId = await NewTicketAsync(technician);

        var response = await TicketClient.AssignAsync(technician, ticketId, Guid.CreateVersion7(), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.assignee_not_found");
        problem.Errors.ShouldNotBeNull().ShouldContainKey("assigneeId");
    }

    /// <summary>
    /// A deactivated technician keeps every ticket they already hold (invariant 9) and
    /// stops being given new ones.
    /// </summary>
    [Fact]
    public async Task A_deactivated_technician_cannot_be_given_a_ticket()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);
        var ticketId = await NewTicketAsync(admin);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ItmsUser>>();
            var account = await users.FindByNameAsync("tech");
            account!.Deactivate(DateTimeOffset.UtcNow, actor: null);
            (await users.UpdateAsync(account)).Succeeded.ShouldBeTrue();
        }

        var response = await TicketClient.AssignAsync(admin, ticketId, techId, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.assignee_inactive");
    }

    /// <summary>
    /// Assigning a ticket to whoever already holds it is refused rather than written,
    /// because it would put a line in the timeline saying it passed from somebody to
    /// themselves.
    /// </summary>
    [Fact]
    public async Task Assigning_the_current_holder_again_is_refused()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);
        var ticketId = await AssignedTicketAsync(technician, techId);

        var response = await TicketClient.AssignAsync(technician, ticketId, techId, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.already_assigned");
    }

    /// <summary>A Closed or Cancelled ticket has no work left to hand anybody.</summary>
    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task A_terminal_ticket_cannot_be_assigned(TicketStatus terminal)
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);
        var ticketId = await NewTicketAsync(technician);
        await TicketWriter.ParkAsync(fixture.DataSource, ticketId, terminal, Token);

        var response = await TicketClient.AssignAsync(technician, ticketId, techId, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.ticket_not_assignable");
    }

    /// <summary>
    /// An explicitly empty id is a client bug, and reading it as "unassign" would carry
    /// out an instruction nobody gave.
    /// </summary>
    [Fact]
    public async Task An_empty_assignee_id_is_a_validation_failure()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var ticketId = await NewTicketAsync(technician);

        var response = await TicketClient.AssignAsync(technician, ticketId, Guid.Empty, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Errors.ShouldNotBeNull().ShouldContainKey("assigneeId");
    }

    /// <summary>
    /// Deciding who works a ticket is a technician's job. A requester may read and (from
    /// WP-1.7) comment on their own ticket, and nothing else — so this is a 403 on the
    /// policy, not a 404 on the row.
    /// </summary>
    [Fact]
    public async Task A_requester_cannot_assign_their_own_ticket()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);
        var endUserId = await TicketClient.UserIdAsync(fixture, "user", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);

        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var ticket = await TicketClient.CreateAsync(
            technician, reference, departmentId, "Laptop will not charge", Token, requesterId: endUserId);

        var response = await TicketClient.AssignAsync(endUser, ticket.Id, techId, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await TicketWriter.StateAsync(fixture.Services, ticket.Id, Token)).Status.ShouldBe(TicketStatus.New);
    }

    [Fact]
    public async Task An_anonymous_caller_is_challenged()
    {
        using var client = fixture.CreateClient();

        var response = await TicketClient.AssignAsync(client, Guid.CreateVersion7(), Guid.CreateVersion7(), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unknown_ticket_answers_404()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);

        var response = await TicketClient.AssignAsync(technician, Guid.CreateVersion7(), techId, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.ticket_not_found");
    }

    /// <summary>
    /// Invariant 3, for the change this package adds: a first assignment writes both the
    /// status line and the assignment line, in the transaction that made the change.
    /// </summary>
    [Fact]
    public async Task A_first_assignment_writes_both_history_lines()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);
        var ticketId = await NewTicketAsync(technician);

        await TicketClient.AssignsAsync(technician, ticketId, techId, Token);

        var entries = await TicketWriter.HistoryAsync(fixture.Services, ticketId, Token);

        entries.Count.ShouldBe(2);
        entries[0].Kind.ShouldBe(TicketChangeKind.Status);
        entries[0].FromValue.ShouldBe("New");
        entries[0].ToValue.ShouldBe("Assigned");
        entries[1].Kind.ShouldBe(TicketChangeKind.Assignment);
        entries[1].FromValue.ShouldBeNull();
        entries[1].ToValue.ShouldNotBeNullOrWhiteSpace();

        // Both lines belong to one change, so they share an instant and are ordered by the
        // ordinal WP-1.4 added for exactly this.
        entries[1].OccurredAt.ShouldBe(entries[0].OccurredAt);
        entries[0].ActorId.ShouldBe(techId);
        entries[1].ActorId.ShouldBe(techId);
    }

    /// <summary>A reassignment moved no status, so it owes one line rather than two.</summary>
    [Fact]
    public async Task A_reassignment_writes_only_the_assignment_line()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);
        var adminId = await TicketClient.UserIdAsync(fixture, "admin", Token);
        var ticketId = await AssignedTicketAsync(technician, techId);

        await TicketClient.AssignsAsync(technician, ticketId, adminId, Token);

        var entries = await TicketWriter.HistoryAsync(fixture.Services, ticketId, Token);

        entries.Count.ShouldBe(3);
        entries[2].Kind.ShouldBe(TicketChangeKind.Assignment);
        entries[2].FromValue.ShouldNotBeNullOrWhiteSpace();
        entries[2].ToValue.ShouldNotBe(entries[2].FromValue);
    }

    /// <summary>
    /// A refused assignment commits nothing — no status, no holder, and no timeline line
    /// claiming otherwise.
    /// </summary>
    [Fact]
    public async Task A_refused_assignment_writes_no_history()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var endUserId = await TicketClient.UserIdAsync(fixture, "user", Token);
        var ticketId = await NewTicketAsync(technician);

        (await TicketClient.AssignAsync(technician, ticketId, endUserId, Token))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await TicketWriter.HistoryAsync(fixture.Services, ticketId, Token)).ShouldBeEmpty();
    }

    /// <summary>
    /// SPEC.md §15 counts assignment changes as mandatory audit coverage. The rows are
    /// built by the Audit module from the events, so they arrive with the dispatcher.
    /// </summary>
    /// <remarks>
    /// The total is asserted exactly, not just the rows this test cares about. A count
    /// that only ever grew would be blind to the failure mode WP-1.3's warning was about:
    /// one change recorded twice under two action names. The third row is
    /// <c>ticket.created</c>, which WP-1.5's create endpoint raised when the ticket was
    /// filed.
    /// </remarks>
    [Fact]
    public async Task A_first_assignment_is_audited_as_an_assignment_and_a_status_change()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);
        var ticketId = await NewTicketAsync(technician);

        await TicketClient.AssignsAsync(technician, ticketId, techId, Token);

        var rows = await AuditRowsAsync(ticketId, expected: 3);

        var assigned = rows.Single(row => row.Action == "ticket.assigned");
        assigned.ActorId.ShouldBe(techId);
        assigned.Changes["assigneeId"].ShouldBe(new(null, techId.ToString()));

        rows.Single(row => row.Action == "ticket.status_changed")
            .Changes["status"].ShouldBe(new("New", "Assigned"));

        rows.Count(row => row.Action == "ticket.created").ShouldBe(1);
    }

    /// <summary>
    /// A reassignment moved no status, so it raises one event and writes one row. The
    /// trail must not claim a ticket went from InProgress to InProgress.
    /// </summary>
    [Fact]
    public async Task A_reassignment_is_audited_as_an_assignment_alone()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);
        var adminId = await TicketClient.UserIdAsync(fixture, "admin", Token);
        var ticketId = await AssignedTicketAsync(technician, techId);

        await TicketClient.AssignsAsync(technician, ticketId, adminId, Token);

        // One from the creation, two from the first assignment, one from this one.
        var rows = await AuditRowsAsync(ticketId, expected: 4);

        rows.Count(row => row.Action == "ticket.assigned").ShouldBe(2);
        rows.Count(row => row.Action == "ticket.status_changed").ShouldBe(1);

        rows.Single(row => row.Action == "ticket.assigned" && row.Changes["assigneeId"].After == adminId.ToString())
            .Changes["assigneeId"].Before.ShouldBe(techId.ToString());
    }

    /// <summary>
    /// The precondition is checked before anything is attempted, so a stale editor is told
    /// to reload rather than being allowed to reassign a ticket somebody else has moved.
    /// </summary>
    [Fact]
    public async Task A_stale_If_Match_is_refused_with_412_and_assigns_nobody()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);
        var adminId = await TicketClient.UserIdAsync(fixture, "admin", Token);
        var ticketId = await NewTicketAsync(technician);
        var (_, stale) = await TicketClient.GetAsync(technician, ticketId, Token);

        // Somebody else picks it up in between, exactly as a second technician would.
        await TicketClient.AssignsAsync(technician, ticketId, techId, Token);

        var response = await TicketClient.AssignAsync(technician, ticketId, adminId, Token, ifMatch: stale);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.ticket_conflict");

        var (ticket, _) = await TicketClient.GetAsync(technician, ticketId, Token);
        ticket.AssigneeId.ShouldBe(techId);
    }

    [Fact]
    public async Task A_matching_If_Match_lets_the_assignment_through()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);
        var ticketId = await NewTicketAsync(technician);
        var (_, etag) = await TicketClient.GetAsync(technician, ticketId, Token);

        var response = await TicketClient.AssignAsync(technician, ticketId, techId, Token, ifMatch: etag);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// <b>The tag the write answers with is the ticket's new one, not its old one.</b>
    /// WP-1.5 left both writes without a tag rather than emit one whose freshness after
    /// <c>SaveChanges</c> had not been checked; this is that check, for both of them. A
    /// stale tag would be worse than none — a client would state a precondition that could
    /// never hold and be refused forever.
    /// </summary>
    [Fact]
    public async Task A_write_answers_with_a_tag_a_following_read_agrees_with()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);
        var ticketId = await NewTicketAsync(technician);
        var (_, beforeTag) = await TicketClient.GetAsync(technician, ticketId, Token);

        var (_, assignmentTag) = await TicketClient.AssignsAsync(technician, ticketId, techId, Token);

        assignmentTag.ShouldNotBeNullOrWhiteSpace();
        assignmentTag.ShouldNotBe(beforeTag);
        (await TicketClient.GetAsync(technician, ticketId, Token)).ETag.ShouldBe(assignmentTag);

        var transition = await TicketClient.ChangeStatusAsync(technician, ticketId, TicketStatus.InProgress, Token);
        transition.EnsureSuccessStatusCode();

        var transitionTag = transition.Headers.ETag?.ToString();
        transitionTag.ShouldNotBeNullOrWhiteSpace();
        transitionTag.ShouldNotBe(assignmentTag);
        (await TicketClient.GetAsync(technician, ticketId, Token)).ETag.ShouldBe(transitionTag);

        // And the fresh tag is immediately usable, which is the whole point of returning it.
        (await TicketClient.AssignAsync(technician, ticketId, techId, Token, ifMatch: transitionTag))
            .StatusCode.ShouldNotBe(HttpStatusCode.PreconditionFailed);
    }

    /// <summary>A raised, unassigned ticket to work with.</summary>
    private async Task<Guid> NewTicketAsync(HttpClient client)
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var ticket = await TicketClient.CreateAsync(
            client, reference, departmentId, "Laptop will not charge", Token);

        return ticket.Id;
    }

    /// <summary>A ticket already assigned to <paramref name="assigneeId"/>, through the real endpoint.</summary>
    private async Task<Guid> AssignedTicketAsync(HttpClient client, Guid assigneeId)
    {
        var ticketId = await NewTicketAsync(client);
        await TicketClient.AssignsAsync(client, ticketId, assigneeId, Token);

        return ticketId;
    }

    /// <summary>The audit rows for a ticket once the dispatcher has delivered them.</summary>
    private async Task<IReadOnlyList<AuditRow>> AuditRowsAsync(Guid ticketId, int expected)
    {
        await Eventually.UntilAsync(
            async () => (await AuditQueries.ByEntityAsync(fixture.DataSource, "Ticket", ticketId.ToString(), Token))
                .Count >= expected,
            $"{expected} audit rows for ticket {ticketId}",
            Token);

        var rows = await AuditQueries.ByEntityAsync(fixture.DataSource, "Ticket", ticketId.ToString(), Token);
        rows.Count.ShouldBe(expected);

        return rows;
    }
}
