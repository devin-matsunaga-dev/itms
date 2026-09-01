using Itms.Contracts.Events;
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

namespace Itms.Modules.Helpdesk.Features.Tickets.ChangeTicketStatus;

/// <summary>
/// Moves a ticket through the state machine SPEC.md §2 defines.
/// </summary>
/// <remarks>
/// <para>
/// The handler decides nothing about which move is legal — <see cref="Ticket.ChangeStatus"/>
/// does, and this reads the answer. That is what invariant 2 requires: the rule lives in
/// the entity, so a second caller arriving in WP-1.6 or WP-1.10 cannot route around it.
/// </para>
/// <para>
/// The change, its history entries, and its outbox events commit together, so a transition
/// that is rolled back leaves nothing claiming it happened. Invariant 3 is what requires
/// that of the history in particular: it is written in the same transaction as the change,
/// not merely soon after it.
/// </para>
/// <para>
/// <b>The audit row is built from the event, not written here.</b> WP-1.3 could not
/// publish — <c>IEventPublisher</c> was still inside the bus, which a module may not
/// reference — so it called <c>IAuditWriter</c> directly and left a standing warning that
/// whichever package started publishing <see cref="TicketStatusChanged"/> had to delete
/// that call in the same diff. WP-1.6 is that package. The Audit module has consumed the
/// event under <c>ticket.status_changed</c> since WP-0.7, and writing both would have
/// recorded every transition twice.
/// </para>
/// <para>
/// <b>What that trade cost, and why <see cref="TicketResolved"/> goes out too.</b> An
/// event-derived audit row carries no source address and no actor name, and
/// <see cref="TicketStatusChanged"/> carries only the two statuses — so the resolution
/// text the direct write used to record would simply have been lost from the trail.
/// <see cref="TicketResolved"/> is what keeps it, and it is the event Phase 4's requester
/// notification needs anyway. The <c>history.RecordAsync</c> call below was <b>not</b>
/// touched: invariant 3 is not satisfied by an audit row, and a consumer reacting to an
/// event cannot write inside the transaction that produced it.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock. Every instant this writes comes from here.</param>
/// <param name="currentUser">Who is making the request.</param>
/// <param name="history">The ticket's own timeline (invariant 3, SPEC.md §2).</param>
/// <param name="publisher">The outbox, enrolled in this handler's own transaction.</param>
internal sealed class ChangeTicketStatusHandler(
    HelpdeskDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    TicketHistoryRecorder history,
    IEventPublisher publisher)
{
    /// <summary>Applies <paramref name="request"/> to the ticket.</summary>
    /// <param name="ticketId">The ticket to move.</param>
    /// <param name="request">The destination, and the resolution notes if it is Resolved.</param>
    /// <param name="expectedVersions">
    /// The row versions the caller's <c>If-Match</c> will accept, or <see langword="null"/>
    /// when it stated no precondition. WP-1.5 added this; a request without the header
    /// behaves exactly as it did before.
    /// </param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>The transition that happened, or the failure that stopped it.</returns>
    public async Task<Result<TicketStatusChange>> HandleAsync(
        Guid ticketId,
        ChangeTicketStatusRequest request,
        IReadOnlySet<uint>? expectedVersions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Error? failure = null;
        TicketStatusChange? change = null;

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

                // The caller's precondition, checked before anything is attempted. That is
                // the whole point of the 412: a stale editor finds out here, having typed
                // nothing, rather than at SaveChanges having typed a resolution. The row is
                // already loaded and locked by the read, so this cannot itself race.
                var entry = database.Entry(ticket);

                if (expectedVersions is not null
                    && !expectedVersions.Contains(entry.Property<uint>(TicketConfiguration.VersionProperty).CurrentValue))
                {
                    failure = HelpdeskErrors.TicketPreconditionFailed();
                    return;
                }

                // Taken before the move, and the only thing this handler has to remember:
                // the recorder works out which entries the change owes from it, so a
                // resolve records both its status move and its resolution without this
                // method knowing that is two lines of the timeline.
                var before = TicketSnapshot.Of(ticket);
                var from = ticket.Status;
                var now = clock.UtcNow;

                var transition = ticket.ChangeStatus(request.Status, request.ResolutionNotes, request.HoldReason, now, currentUser.UserId);

                if (transition.IsFailure)
                {
                    failure = transition.Error;
                    return;
                }

                // Added, not saved: the entries go to the database on the SaveChanges below,
                // in one command batch with the ticket update and inside the one transaction
                // this handler opened. That is invariant 3, and it is what makes a rolled
                // back transition incapable of leaving an orphan line behind.
                await history.RecordAsync(ticket, before, now, token).ConfigureAwait(false);

                try
                {
                    await database.SaveChangesAsync(token).ConfigureAwait(false);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Somebody moved the ticket between the read and the write. A 409 the
                    // client can retry, not the 500 an unhandled one would be. WP-1.5 turns
                    // the same token into an ETag so the conflict can also be caught before
                    // the work is done.
                    failure = HelpdeskErrors.TicketChangedConcurrently();
                    return;
                }

                await PublishAsync(ticket, from, now, token).ConfigureAwait(false);

                change = new TicketStatusChange(
                    new TicketStatusChangeResponse(
                        ticket.Id,
                        ticket.Number,
                        from,
                        ticket.Status,
                        now,
                        ticket.ResolvedAt,
                        ticket.ClosedAt,
                        TicketStateMachine.DestinationsFrom(ticket.Status)),
                    // WP-1.5 left this response without a tag because EF's refresh of the
                    // xmin shadow property after SaveChanges had not been verified, and a
                    // stale tag is worse than none. It is verified now: the property is
                    // ValueGeneratedOnAddOrUpdate, so the UPDATE returns the new value and
                    // EF writes it back here. TicketETagTests asserts it against a read.
                    entry.Property<uint>(TicketConfiguration.VersionProperty).CurrentValue);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is null
            ? Result.Success(change!)
            : Result.Failure<TicketStatusChange>(failure);
    }

    /// <summary>
    /// Stages the facts this transition produced into the transaction that produced them.
    /// </summary>
    /// <remarks>
    /// <see cref="TicketResolved"/> is published alongside, not instead: a consumer that
    /// cares about resolution — the requester's notification, the knowledge-base
    /// suggestion, the audit row that records what was actually done — should not have to
    /// string-match a status name to find it, which is the reason the event exists.
    /// </remarks>
    private async Task PublishAsync(
        Ticket ticket,
        TicketStatus from,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            new TicketStatusChanged(ticket.Id, ticket.Number, from.ToString(), ticket.Status.ToString())
            {
                // Stamped explicitly: the dispatcher runs on a background scope with no
                // principal, so the actor the audit trail records is the one named here.
                // Without this the trail would say a ticket moved and not who moved it,
                // which SPEC.md §15 counts as mandatory coverage.
                ActorId = currentUser.UserId,
                OccurredAt = now,
            },
            cancellationToken).ConfigureAwait(false);

        if (ticket.Status != TicketStatus.Resolved)
        {
            return;
        }

        await publisher.PublishAsync(
            new TicketResolved(
                ticket.Id,
                ticket.Number,
                ticket.RequesterId,
                ticket.ResolvedAt!.Value,
                ticket.ResolutionNotes!)
            {
                ActorId = currentUser.UserId,
                OccurredAt = now,
            },
            cancellationToken).ConfigureAwait(false);
    }
}
