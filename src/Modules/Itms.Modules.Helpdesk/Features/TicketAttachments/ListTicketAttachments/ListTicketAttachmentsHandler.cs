using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.Tickets;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.TicketAttachments.ListTicketAttachments;

/// <summary>Reads one ticket's attachments, newest first.</summary>
/// <remarks>
/// The same two filters as the comment thread, for the same two reasons: the ticket is
/// narrowed by <see cref="TicketScope"/> so somebody else's files are a 404, and the rows
/// by <see cref="TicketVisibility"/> so a requester never learns that an internal file
/// exists. The total is counted after the second filter, so it does not announce in a
/// number what the list withheld.
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="currentUser">Who is asking.</param>
internal sealed class ListTicketAttachmentsHandler(HelpdeskDbContext database, ICurrentUser currentUser)
{
    /// <summary>Reads a page of the attachment list.</summary>
    /// <param name="ticketId">The ticket whose attachments are wanted.</param>
    /// <param name="page">The page to read.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The page envelope, or not-found for a ticket the caller may not see.</returns>
    public async Task<Result<PagedResult<TicketAttachmentResponse>>> HandleAsync(
        Guid ticketId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var visible = await database.Tickets
            .AsNoTracking()
            .VisibleTo(currentUser)
            .AnyAsync(ticket => ticket.Id == ticketId, cancellationToken)
            .ConfigureAwait(false);

        if (!visible)
        {
            return Result.Failure<PagedResult<TicketAttachmentResponse>>(HelpdeskErrors.TicketNotFound());
        }

        var query = database.TicketAttachments
            .AsNoTracking()
            .Where(attachment => attachment.TicketId == ticketId)
            .VisibleTo(currentUser);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(attachment => attachment.CreatedAt)
            .ThenByDescending(attachment => attachment.Id)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(TicketAttachmentResponse.Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.From<TicketAttachmentResponse>(items, total, page);
    }
}
