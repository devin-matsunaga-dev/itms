using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.Tickets;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.TicketComments.ListTicketComments;

/// <summary>Reads one ticket's conversation, newest first.</summary>
/// <remarks>
/// <para>
/// <b>Both filters, in both places.</b> The ticket is narrowed by
/// <see cref="TicketScope"/> so somebody else's thread is a 404, and the comments are
/// narrowed by <see cref="TicketVisibility"/> so a requester's own thread contains no
/// internal note. Neither substitutes for the other, and this is the first read in the
/// system that needs both.
/// </para>
/// <para>
/// <b>The total counts what the caller can see.</b> It is computed after the visibility
/// filter, so a requester on a ticket with two public comments and three notes is told
/// there are two. A total that counted all five would announce the notes' existence in a
/// number, which is the same leak as showing them.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="currentUser">Who is asking. Decides both which tickets and which lines.</param>
internal sealed class ListTicketCommentsHandler(HelpdeskDbContext database, ICurrentUser currentUser)
{
    /// <summary>Reads a page of the thread.</summary>
    /// <param name="ticketId">The ticket whose comments are wanted.</param>
    /// <param name="page">The page to read.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The page envelope, or not-found for a ticket the caller may not see.</returns>
    public async Task<Result<PagedResult<TicketCommentResponse>>> HandleAsync(
        Guid ticketId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        // Asked against the ticket rather than the comments, following the timeline: a
        // ticket nobody has commented on answers with an empty page, and a ticket the
        // caller may not see answers 404. Reading the comments alone could not tell an
        // empty thread from a forbidden one.
        var visible = await database.Tickets
            .AsNoTracking()
            .VisibleTo(currentUser)
            .AnyAsync(ticket => ticket.Id == ticketId, cancellationToken)
            .ConfigureAwait(false);

        if (!visible)
        {
            return Result.Failure<PagedResult<TicketCommentResponse>>(HelpdeskErrors.TicketNotFound());
        }

        var query = database.TicketComments
            .AsNoTracking()
            .Where(comment => comment.TicketId == ticketId)
            .VisibleTo(currentUser);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(comment => comment.CreatedAt)
            // The id breaks the tie, for WP-1.5's reason: two comments can share an instant,
            // no other column is unique, and a paged list whose order changes between reads
            // silently drops and duplicates rows across page boundaries. A version 7 id is
            // time-ordered between milliseconds, so it agrees with the instant it is
            // breaking ties within.
            .ThenByDescending(comment => comment.Id)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(TicketCommentResponse.Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.From<TicketCommentResponse>(items, total, page);
    }
}
