using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.Tickets;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.TicketHistory.ListTicketHistory;

/// <summary>Reads one ticket's timeline, newest first.</summary>
/// <remarks>
/// <para>
/// <b>WP-1.5 widened this to the requester</b>, at the human's direction. WP-1.4 left the
/// timeline on the Technician policy because it had no requester-scoped read to answer the
/// row-level question against; the detail endpoint is that read, and the two now apply the
/// same <see cref="TicketScope"/>. A User sees their own ticket's history and nobody
/// else's, and a ticket they did not raise is a 404 here exactly as it is there.
/// </para>
/// <para>
/// <b>WP-1.7 has to keep that true.</b> The four kinds this timeline carries — status,
/// priority, assignment, resolution — are all things the requester can already see on the
/// ticket. An internal note is not, and the moment a note can produce a timeline entry,
/// scoping the ticket stops being sufficient and the entries themselves need filtering.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="currentUser">Who is asking. Decides whether this ticket exists for them at all.</param>
internal sealed class ListTicketHistoryHandler(HelpdeskDbContext database, ICurrentUser currentUser)
{
    /// <summary>Reads a page of the timeline.</summary>
    /// <param name="ticketId">The ticket whose history is wanted.</param>
    /// <param name="page">The page to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The page envelope, or a not-found failure when there is no such ticket.</returns>
    public async Task<Result<PagedResult<TicketHistoryEntryResponse>>> HandleAsync(
        Guid ticketId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        // Asked first, and against the ticket rather than the history, so a ticket that
        // exists and has not moved yet answers with an empty page while one that does not
        // exist answers 404. Reading the history alone could not tell those apart, and an
        // empty timeline for a ticket number nobody has is the more misleading of the two.
        // The soft-delete filter applies, so a deleted ticket is a 404 here too — and so
        // does the row-level scope, which is what makes somebody else's ticket a 404
        // rather than an empty timeline that confirms it exists.
        var exists = await database.Tickets
            .AsNoTracking()
            .VisibleTo(currentUser)
            .AnyAsync(ticket => ticket.Id == ticketId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            return Result.Failure<PagedResult<TicketHistoryEntryResponse>>(HelpdeskErrors.TicketNotFound());
        }

        var query = database.TicketHistory
            .AsNoTracking()
            .Where(entry => entry.TicketId == ticketId);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Newest first. One change can write more than one entry — resolving moves the
        // status and records the resolution — and those entries share an instant because they
        // genuinely happened at one, so the ordinal within the change is what orders them.
        // The id is a last tiebreaker only, for the case of two changes landing on the same
        // instant; without all three, a timeline could come back in a different order on the
        // second read of the same data, which is precisely what makes a paged list unusable.
        var items = await query
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Sequence)
            .ThenByDescending(entry => entry.Id)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(TicketHistoryEntryResponse.Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.From<TicketHistoryEntryResponse>(items, total, page);
    }
}
