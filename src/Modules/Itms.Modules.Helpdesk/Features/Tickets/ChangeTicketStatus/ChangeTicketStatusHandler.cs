using Itms.Contracts.Auditing;
using Itms.Modules.Helpdesk.Auditing;
using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.TicketHistory;
using Itms.Modules.Helpdesk.Persistence;
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
/// The change, its history entries, and its audit row commit together, so a transition
/// that is rolled back leaves nothing claiming it happened. Invariant 3 is what requires
/// that of the history in particular: it is written in the same transaction as the change,
/// not merely soon after it.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock. Every instant this writes comes from here.</param>
/// <param name="currentUser">Who is making the request.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8, SPEC.md §15).</param>
/// <param name="history">The ticket's own timeline (invariant 3, SPEC.md §2).</param>
internal sealed class ChangeTicketStatusHandler(
    HelpdeskDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit,
    TicketHistoryRecorder history)
{
    /// <summary>Applies <paramref name="request"/> to the ticket.</summary>
    /// <param name="ticketId">The ticket to move.</param>
    /// <param name="request">The destination, and the resolution notes if it is Resolved.</param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>The transition that happened, or the failure that stopped it.</returns>
    public async Task<Result<TicketStatusChangeResponse>> HandleAsync(
        Guid ticketId,
        ChangeTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Error? failure = null;
        TicketStatusChangeResponse? response = null;

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

                // Taken before the move, and the only thing this handler has to remember:
                // the recorder works out which entries the change owes from it, so a
                // resolve records both its status move and its resolution without this
                // method knowing that is two lines of the timeline.
                var before = TicketSnapshot.Of(ticket);
                var from = ticket.Status;
                var resolvedBefore = ticket.ResolvedAt;
                var closedBefore = ticket.ClosedAt;
                var notesBefore = ticket.ResolutionNotes;
                var now = clock.UtcNow;

                var transition = ticket.ChangeStatus(request.Status, request.ResolutionNotes, now, currentUser.UserId);

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

                await audit.WriteAsync(
                    new AuditEntry(
                        HelpdeskAudit.TicketStatusChanged,
                        HelpdeskAudit.TicketEntityType,
                        ticket.Id.ToString(),
                        HelpdeskAudit.Changes()
                            .Moved("status", from.ToString(), ticket.Status.ToString())
                            .Moved("resolvedAt", Instant(resolvedBefore), Instant(ticket.ResolvedAt))
                            .Moved("closedAt", Instant(closedBefore), Instant(ticket.ClosedAt))
                            .Moved("resolutionNotes", notesBefore, ticket.ResolutionNotes)),
                    token).ConfigureAwait(false);

                response = new TicketStatusChangeResponse(
                    ticket.Id,
                    ticket.Number,
                    from,
                    ticket.Status,
                    now,
                    ticket.ResolvedAt,
                    ticket.ClosedAt,
                    TicketStateMachine.DestinationsFrom(ticket.Status));
            },
            cancellationToken).ConfigureAwait(false);

        return failure is null
            ? Result.Success(response!)
            : Result.Failure<TicketStatusChangeResponse>(failure);
    }

    /// <summary>An instant as the audit diff records it, or null when it was not set.</summary>
    /// <remarks>Round-trip format, because an audit value is read by machines as well as people.</remarks>
    private static string? Instant(DateTimeOffset? value) =>
        value?.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
}
