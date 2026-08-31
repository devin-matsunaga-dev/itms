using Itms.Contracts.Events;
using Itms.Contracts.Lookups;
using Itms.Contracts.Messaging;
using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.TicketHistory;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Modules.Helpdesk.Persistence.Configurations;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.Tickets.AssignTicket;

/// <summary>
/// Puts a technician in charge of a ticket, hands it to a different one, or takes it back
/// off them.
/// </summary>
/// <remarks>
/// <para>
/// <b>The handler decides nothing about whether the move is allowed.</b>
/// <see cref="Ticket.Assign"/> and <see cref="Ticket.Unassign"/> do, and this reads the
/// answer — the same division WP-1.3 drew for the status change, and for the same reason:
/// the rule that a ticket in <c>Assigned</c> always has an assignee has to live where it
/// cannot be routed around.
/// </para>
/// <para>
/// <b>Who may be assigned is the one question the entity cannot answer.</b> Whether an
/// account exists, is active, and holds the Technician or Admin role are facts about
/// Identity's rows, so they are read here through <see cref="IUserLookup"/> — the second
/// cross-module read in the system after WP-1.5's — and the name is cached onto the ticket
/// per §3 rule 6.
/// </para>
/// <para>
/// <b>Two events, and both are facts.</b> <see cref="TicketAssigned"/> says who holds it;
/// <see cref="TicketStatusChanged"/> says the workflow moved, and is published only when
/// it actually did — a reassignment moves nobody's status and raises only the first. The
/// audit trail is built from them by the Audit module's consumer, which is why there is no
/// <c>IAuditWriter</c> call here (WP-1.5 made the same call for <c>TicketCreated</c>).
/// </para>
/// <para>
/// The ticket, its history, and both outbox rows commit in one transaction, so an
/// assignment that is rolled back leaves nothing claiming it happened.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock. Every instant this writes comes from here.</param>
/// <param name="currentUser">Who is making the request.</param>
/// <param name="users">Identity's public contract, for the assignee's name and roles.</param>
/// <param name="history">The ticket's own timeline (invariant 3, SPEC.md §2).</param>
/// <param name="publisher">The outbox, enrolled in this handler's own transaction.</param>
internal sealed class AssignTicketHandler(
    HelpdeskDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IUserLookup users,
    TicketHistoryRecorder history,
    IEventPublisher publisher)
{
    /// <summary>Applies <paramref name="request"/> to the ticket.</summary>
    /// <param name="ticketId">The ticket whose assignee is changing.</param>
    /// <param name="request">Who is taking it on, or null to unassign.</param>
    /// <param name="expectedVersions">
    /// The row versions the caller's <c>If-Match</c> will accept, or <see langword="null"/>
    /// when it stated no precondition.
    /// </param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>The assignment that happened, or the failure that stopped it.</returns>
    public async Task<Result<TicketAssignment>> HandleAsync(
        Guid ticketId,
        AssignTicketRequest request,
        IReadOnlySet<uint>? expectedVersions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Read before the transaction, following WP-1.5: this is a cross-module read, and
        // holding a row lock on the ticket across it would serialise assignment behind
        // Identity.
        var assignee = await ResolveAssigneeAsync(request.AssigneeId, cancellationToken).ConfigureAwait(false);

        if (assignee.IsFailure)
        {
            return Result.Failure<TicketAssignment>(assignee.Error!);
        }

        Error? failure = null;
        TicketAssignment? assignment = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                // Tracked, not AsNoTracking: this is a write, and the xmin token WP-1.2
                // mapped only does its job on a tracked entity.
                var ticket = await database.Tickets
                    .FirstOrDefaultAsync(candidate => candidate.Id == ticketId, token)
                    .ConfigureAwait(false);

                if (ticket is null)
                {
                    failure = HelpdeskErrors.TicketNotFound();
                    return;
                }

                var entry = database.Entry(ticket);

                // The caller's precondition, checked before anything is attempted — the
                // whole point of the 412. The row is already loaded and locked by the read,
                // so this cannot itself race.
                if (expectedVersions is not null
                    && !expectedVersions.Contains(entry.Property<uint>(TicketConfiguration.VersionProperty).CurrentValue))
                {
                    failure = HelpdeskErrors.TicketPreconditionFailed();
                    return;
                }

                // Taken before the move: the recorder works out which entries the change
                // owes by comparing this with the ticket afterwards, so this handler never
                // has to name its own history lines.
                var before = TicketSnapshot.Of(ticket);
                var fromStatus = ticket.Status;
                var now = clock.UtcNow;

                var moved = assignee.Value is { } technician
                    ? ticket.Assign(technician.Id, technician.DisplayName, now, currentUser.UserId)
                    : ticket.Unassign(now, currentUser.UserId);

                if (moved.IsFailure)
                {
                    failure = moved.Error;
                    return;
                }

                // Added, not saved: the entries reach the database on the SaveChanges
                // below, inside this transaction. That is invariant 3.
                await history.RecordAsync(ticket, before, now, token).ConfigureAwait(false);

                try
                {
                    await database.SaveChangesAsync(token).ConfigureAwait(false);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Somebody moved the ticket between the read and the write. A 409 the
                    // client can retry, not the 500 an unhandled one would be.
                    failure = HelpdeskErrors.TicketChangedConcurrently();
                    return;
                }

                await PublishAsync(ticket, before, fromStatus, now, token).ConfigureAwait(false);

                assignment = new TicketAssignment(
                    new TicketAssignmentResponse(
                        ticket.Id,
                        ticket.Number,
                        before.AssigneeId,
                        before.AssigneeName,
                        ticket.AssigneeId,
                        ticket.AssigneeName,
                        fromStatus,
                        ticket.Status,
                        now,
                        TicketStateMachine.DestinationsFrom(ticket.Status)),
                    // Read back off the tracked entry rather than from before the write:
                    // xmin is ValueGeneratedOnAddOrUpdate, so EF returns the new value with
                    // the UPDATE and refreshes it here. A stale tag would be worse than no
                    // tag, so TicketETagTests asserts this one against a following read.
                    entry.Property<uint>(TicketConfiguration.VersionProperty).CurrentValue);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is null
            ? Result.Success(assignment!)
            : Result.Failure<TicketAssignment>(failure);
    }

    /// <summary>
    /// Stages the facts this change produced into the transaction that produced them.
    /// </summary>
    /// <remarks>
    /// <see cref="TicketStatusChanged"/> goes out only when the status actually moved,
    /// which is a first assignment or an unassignment and never a reassignment. Publishing
    /// it unconditionally would put a row in the audit trail saying a ticket went from
    /// InProgress to InProgress.
    /// </remarks>
    private async Task PublishAsync(
        Ticket ticket,
        TicketSnapshot before,
        TicketStatus fromStatus,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            new TicketAssigned(ticket.Id, ticket.Number, ticket.AssigneeId, before.AssigneeId)
            {
                // Stamped explicitly: the dispatcher runs on a background scope with no
                // principal, so the actor the audit trail records is the one named here.
                ActorId = currentUser.UserId,
                OccurredAt = now,
            },
            cancellationToken).ConfigureAwait(false);

        if (fromStatus == ticket.Status)
        {
            return;
        }

        await publisher.PublishAsync(
            new TicketStatusChanged(ticket.Id, ticket.Number, fromStatus.ToString(), ticket.Status.ToString())
            {
                ActorId = currentUser.UserId,
                OccurredAt = now,
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Finds the account a ticket is being handed to, or establishes that it is being
    /// handed to nobody.
    /// </summary>
    /// <remarks>
    /// The role check is the reason <c>UserSummary</c> carries roles at all. It is done
    /// here rather than in the picker because ARCHITECTURE.md §7 is explicit that the
    /// React app hiding what a role cannot do is never the enforcement — a ticket assigned
    /// to an end user would sit in a queue they have no route to work.
    /// </remarks>
    /// <param name="assigneeId">The account named in the request, or null to unassign.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The assignee, null for an unassignment, or the failure that refuses it.</returns>
    private async Task<Result<UserSummary?>> ResolveAssigneeAsync(
        Guid? assigneeId,
        CancellationToken cancellationToken)
    {
        if (assigneeId is not { } id)
        {
            return Result.Success<UserSummary?>(null);
        }

        var user = await users.GetAsync(id, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            return Result.Failure<UserSummary?>(HelpdeskErrors.AssigneeNotFound());
        }

        if (!user.IsActive)
        {
            return Result.Failure<UserSummary?>(HelpdeskErrors.AssigneeInactive());
        }

        // Admin as well as Technician, matching TicketScope.SeesEveryTicket: an
        // administrator working the queue is a person the queue can be given to.
        var works = user.Roles.Contains(ItmsRoles.Technician, StringComparer.Ordinal)
            || user.Roles.Contains(ItmsRoles.Admin, StringComparer.Ordinal);

        return works
            ? Result.Success<UserSummary?>(user)
            : Result.Failure<UserSummary?>(HelpdeskErrors.AssigneeNotTechnician());
    }
}
