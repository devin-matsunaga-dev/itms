using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Identity;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.TicketHistory;

/// <summary>
/// Writes a ticket's timeline. Every handler that moves a status, a priority, an
/// assignment, or a resolution goes through here.
/// </summary>
/// <remarks>
/// <para>
/// <b>It adds, it does not save.</b> The entries are added to the caller's own
/// <see cref="HelpdeskDbContext"/> and go to the database on the caller's own
/// <c>SaveChangesAsync</c>, inside the caller's own transaction. That is what invariant 3
/// asks for — the history entry written in the same transaction as the change — and it is
/// why a rolled-back change cannot leave an orphan line claiming it happened. A recorder
/// that saved for itself would be a second commit, and the whole guarantee would be gone.
/// </para>
/// <para>
/// <b>Who and when come from here, what moved comes from
/// <see cref="TicketChanges.Between"/>.</b> The actor is read from
/// <see cref="ICurrentUser"/> rather than passed in, so no call site can attribute a
/// change to somebody else by accident; the instant is passed in, because it has to be
/// the same one the change itself wrote and the handler already has it.
/// </para>
/// <para>
/// Public rather than internal, for the reason WP-1.3 made <see cref="TicketChanges"/>'s
/// neighbour <c>TicketStateMachine</c> public: the integration suite has to prove that a
/// rolled-back transaction leaves no orphan line, and the only way to prove that against
/// the recorder callers actually use is to drive it from a transaction the test controls.
/// No module can reference Helpdesk anyway, and the alternative is an
/// <c>InternalsVisibleTo</c> this repository has nowhere else.
/// </para>
/// </remarks>
/// <param name="database">The caller's helpdesk context, already enlisted in its transaction.</param>
/// <param name="currentUser">Who is making the request.</param>
public sealed class TicketHistoryRecorder(HelpdeskDbContext database, ICurrentUser currentUser)
{
    /// <summary>What a priority name reads as when the row behind it cannot be found.</summary>
    private const string UnknownPriority = "(unknown priority)";

    /// <summary>
    /// Adds the history entries owed for the move from <paramref name="before"/> to the
    /// ticket as it now stands.
    /// </summary>
    /// <remarks>
    /// Nothing is added when nothing tracked moved. A caller does not have to check first:
    /// a change that only touched the subject writes no timeline line, which is correct.
    /// </remarks>
    /// <param name="ticket">The ticket, after the change has been applied to it.</param>
    /// <param name="before">The snapshot taken before the change.</param>
    /// <param name="occurredAt">When the change happened (UTC) — the same instant the change wrote.</param>
    /// <param name="cancellationToken">Cancels the priority-name lookup.</param>
    /// <returns>The entries added, in timeline order. Empty when nothing tracked moved.</returns>
    public async Task<IReadOnlyList<TicketHistoryEntry>> RecordAsync(
        Ticket ticket,
        TicketSnapshot before,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var after = TicketSnapshot.Of(ticket);

        // Looked up only when the priority actually moved. Every other tracked value is
        // already on the ticket row; this one lives on another row, and reading it on every
        // status change would be a query per transition to describe something that did not
        // happen.
        TicketPriorityNames? priorityNames = before.PriorityId == after.PriorityId
            ? null
            : await PriorityNamesAsync(before.PriorityId, after.PriorityId, cancellationToken).ConfigureAwait(false);

        var changes = TicketChanges.Between(before, after, priorityNames);

        if (changes.Count == 0)
        {
            return [];
        }

        // The ordinal is what orders the lines a single change writes: they share an
        // instant, so nothing else can. TicketChanges.Between fixes the order they come in.
        var entries = changes
            .Select((change, sequence) => TicketHistoryEntry.Record(
                ticket.Id,
                change,
                sequence,
                occurredAt,
                currentUser.UserId,
                currentUser.DisplayName))
            .ToList();

        await database.TicketHistory.AddRangeAsync(entries, cancellationToken).ConfigureAwait(false);

        return entries;
    }

    /// <summary>
    /// Reads the names both priorities carry right now, which is what the entry records
    /// as their names at the time.
    /// </summary>
    /// <remarks>
    /// A priority that has since been hard-deleted cannot be — there is no delete path, and
    /// <c>fk_tickets_priority_id</c> restricts one anyway — so the only way a name is
    /// missing is a priority that never existed, which the caller's own validation would
    /// have refused. The fallback keeps a broken lookup from losing the whole entry.
    /// </remarks>
    private async Task<TicketPriorityNames> PriorityNamesAsync(
        Guid fromId,
        Guid toId,
        CancellationToken cancellationToken)
    {
        var names = await database.TicketPriorities
            .AsNoTracking()
            .Where(priority => priority.Id == fromId || priority.Id == toId)
            .Select(priority => new { priority.Id, priority.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TicketPriorityNames(
            names.Find(candidate => candidate.Id == fromId)?.Name ?? UnknownPriority,
            names.Find(candidate => candidate.Id == toId)?.Name ?? UnknownPriority);
    }
}
