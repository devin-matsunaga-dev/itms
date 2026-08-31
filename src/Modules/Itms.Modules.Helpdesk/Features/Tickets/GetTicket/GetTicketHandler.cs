using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.TicketAttachments;
using Itms.Modules.Helpdesk.Features.TicketComments;
using Itms.Modules.Helpdesk.Features.TicketHistory;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.Tickets.GetTicket;

/// <summary>Reads one ticket in full.</summary>
/// <remarks>
/// <para>
/// Four queries, deliberately: the ticket with its reference data, then the heads of its
/// timeline, its conversation, and its attachments. They are separate because each of the
/// three is a one-to-many — folding them into the first would multiply the ticket's columns
/// by its history by its comments and hand the client the same subject several hundred
/// times.
/// </para>
/// <para>
/// All three are embedded rather than linked, at the human's direction, so the normal
/// detail view is one round trip. A list longer than its embedded count sets its
/// <c>hasMore…</c> flag and the client pages on through that list's own endpoint.
/// </para>
/// <para>
/// <b>The conversation and the attachments are filtered by
/// <see cref="TicketVisibility"/> before they are counted, not after.</b> A requester's
/// detail contains no internal note, no internal attachment, and no flag or count implying
/// either exists — which is why <c>hasMoreComments</c> is decided from the filtered query
/// rather than from the ticket's true total. <see cref="TicketScope"/> alone would not have
/// caught this: it says which tickets they may read, and this is their own.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="currentUser">Who is asking. Decides whether this ticket exists for them at all.</param>
/// <param name="clock">The system clock, for the SLA states. The stored instants come from the row; where they stand comes from here.</param>
internal sealed class GetTicketHandler(HelpdeskDbContext database, ICurrentUser currentUser, IClock clock)
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

        // Scoped to what this caller may read before the page is taken, so the "one more
        // than the page" trick counts visible rows and the flag cannot betray a hidden one.
        var comments = await database.TicketComments
            .AsNoTracking()
            .Where(comment => comment.TicketId == ticketId)
            .VisibleTo(currentUser)
            .OrderByDescending(comment => comment.CreatedAt)
            .ThenByDescending(comment => comment.Id)
            .Take(TicketDetailResponse.EmbeddedThreadCount + 1)
            .Select(TicketCommentResponse.Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMoreComments = Trim(comments);

        var attachments = await database.TicketAttachments
            .AsNoTracking()
            .Where(attachment => attachment.TicketId == ticketId)
            .VisibleTo(currentUser)
            .OrderByDescending(attachment => attachment.CreatedAt)
            .ThenByDescending(attachment => attachment.Id)
            .Take(TicketDetailResponse.EmbeddedThreadCount + 1)
            .Select(TicketAttachmentResponse.Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMoreAttachments = Trim(attachments);

        return Result.Success(detail with
        {
            // Assessed first, so the `with` below cannot drop it: the SLA states are the
            // one part of this response the projection could not bring back.
            Response = detail.Response.Assessed(clock.UtcNow) with
            {
                // From the state machine, never restated by the client: WP-1.10's buttons
                // come from here, so they cannot drift from what the server will accept.
                AllowedNextStatuses = TicketStateMachine.DestinationsFrom(detail.Response.Status),
                History = entries,
                HasMoreHistory = hasMore,
                Comments = comments,
                HasMoreComments = hasMoreComments,
                Attachments = attachments,
                HasMoreAttachments = hasMoreAttachments,
            },
        });
    }

    /// <summary>
    /// Drops the extra row read to answer "is there more?", and says whether there was one.
    /// </summary>
    /// <remarks>
    /// One row over the page beats a second <c>COUNT</c>: the flag is the only thing the
    /// detail needs, and the list endpoints carry the real total for anybody who wants it.
    /// </remarks>
    /// <typeparam name="T">The projected row type.</typeparam>
    /// <param name="rows">The rows read, one longer than the page if there are more.</param>
    /// <returns><see langword="true"/> when a row was dropped.</returns>
    private static bool Trim<T>(List<T> rows)
    {
        if (rows.Count <= TicketDetailResponse.EmbeddedThreadCount)
        {
            return false;
        }

        rows.RemoveAt(rows.Count - 1);

        return true;
    }
}
