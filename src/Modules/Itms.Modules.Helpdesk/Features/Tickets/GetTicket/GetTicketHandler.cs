using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.TicketHistory;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.Tickets.GetTicket;

/// <summary>Reads one ticket in full.</summary>
/// <remarks>
/// <para>
/// Two queries, deliberately: the ticket with its reference data, then the head of its
/// timeline. They are separate because the second is a one-to-many — folding it into the
/// first would multiply the ticket's columns by its history and hand the client the same
/// subject twenty-five times.
/// </para>
/// <para>
/// The timeline is embedded rather than linked, at the human's direction, so the normal
/// detail view is one round trip. A ticket with a longer history than
/// <see cref="TicketDetailResponse.EmbeddedHistoryCount"/> sets
/// <c>hasMoreHistory</c> and the client pages on through
/// <c>GET /api/v1/tickets/{id}/history</c>.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="currentUser">Who is asking. Decides whether this ticket exists for them at all.</param>
internal sealed class GetTicketHandler(HelpdeskDbContext database, ICurrentUser currentUser)
{
    /// <summary>Reads the ticket.</summary>
    /// <param name="ticketId">The ticket wanted.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>
    /// The ticket and the row version to build its ETag from, or not-found — which is also
    /// the answer for a ticket the caller may not see.
    /// </returns>
    public async Task<Result<TicketDetail>> HandleAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var scoped = database.Tickets
            .AsNoTracking()
            .VisibleTo(currentUser)
            .Where(ticket => ticket.Id == ticketId);

        var detail = await TicketDetailResponse
            .Project(scoped, database)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (detail is null)
        {
            return Result.Failure<TicketDetail>(HelpdeskErrors.TicketNotFound());
        }

        // One more than the page, so "is there more?" is answered without a second COUNT
        // over a table that will be the largest in the module.
        var entries = await database.TicketHistory
            .AsNoTracking()
            .Where(entry => entry.TicketId == ticketId)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Sequence)
            .ThenByDescending(entry => entry.Id)
            .Take(TicketDetailResponse.EmbeddedHistoryCount + 1)
            .Select(TicketHistoryEntryResponse.Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMore = entries.Count > TicketDetailResponse.EmbeddedHistoryCount;

        if (hasMore)
        {
            entries.RemoveAt(entries.Count - 1);
        }

        return Result.Success(detail with
        {
            Response = detail.Response with
            {
                // From the state machine, never restated by the client: WP-1.10's buttons
                // come from here, so they cannot drift from what the server will accept.
                AllowedNextStatuses = TicketStateMachine.DestinationsFrom(detail.Response.Status),
                History = entries,
                HasMoreHistory = hasMore,
            },
        });
    }
}
