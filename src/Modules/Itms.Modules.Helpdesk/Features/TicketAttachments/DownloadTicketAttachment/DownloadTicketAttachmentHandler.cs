using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.Tickets;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.TicketAttachments.DownloadTicketAttachment;

/// <summary>An open stream over an attachment's bytes, with what the response has to declare.</summary>
/// <remarks>
/// The caller owns the stream and disposes it — in practice the framework does, because the
/// endpoint hands it straight to <c>Results.Stream</c>.
/// </remarks>
/// <param name="Content">The bytes, open for reading.</param>
/// <param name="FileName">The name to offer the file under.</param>
/// <param name="ContentType">The media type, derived at upload from the validated extension.</param>
/// <param name="ByteLength">The length recorded when it was stored.</param>
internal sealed record AttachmentDownload(
    Stream Content,
    string FileName,
    string ContentType,
    long ByteLength);

/// <summary>
/// Serves one attachment's bytes, re-checking on every fetch that the caller may have them.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the endpoint CONVENTIONS.md's security floor is describing</b> when it says
/// uploads are "served through an authorized endpoint that re-checks permission". Nothing
/// about the earlier list is trusted: a caller who was shown an attachment id, or guessed
/// one, gets it only if this query — scoped by <see cref="TicketScope"/> and filtered by
/// <see cref="TicketVisibility"/> — returns the row again now.
/// </para>
/// <para>
/// <b>The ticket id in the route is part of the check, not decoration.</b> The row must
/// belong to the ticket named, so an attachment id cannot be fetched through a ticket the
/// caller happens to be allowed to read.
/// </para>
/// <para>
/// <b>The permission decision happens before the store is asked anything.</b> The stored
/// name is inside the projection, so a caller who may not have the file never causes a path
/// to be built from it, let alone a file to be opened.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="currentUser">Who is asking.</param>
/// <param name="store">Where the bytes are.</param>
internal sealed class DownloadTicketAttachmentHandler(
    HelpdeskDbContext database,
    ICurrentUser currentUser,
    IAttachmentStore store)
{
    /// <summary>Opens the attachment for the caller.</summary>
    /// <param name="ticketId">The ticket it must belong to.</param>
    /// <param name="attachmentId">The attachment wanted.</param>
    /// <param name="cancellationToken">Cancels the query and the open.</param>
    /// <returns>
    /// The open stream and its metadata; not-found when there is no such attachment, it is
    /// on another ticket, the ticket is not the caller's to read, or it is internal and the
    /// caller is not staff — all one answer, deliberately.
    /// </returns>
    public async Task<Result<AttachmentDownload>> HandleAsync(
        Guid ticketId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        // The ticket join is what makes the route's ticket id part of the permission
        // decision rather than a label: an attachment reached through the wrong ticket is
        // not found, even for somebody who could have reached it through the right one.
        var found = await database.TicketAttachments
            .AsNoTracking()
            .Where(attachment => attachment.Id == attachmentId && attachment.TicketId == ticketId)
            .VisibleTo(currentUser)
            .Where(attachment => database.Tickets.VisibleTo(currentUser).Any(ticket => ticket.Id == attachment.TicketId))
            .Select(attachment => new
            {
                attachment.StoredName,
                attachment.FileName,
                attachment.ContentType,
                attachment.ByteLength,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (found is null)
        {
            return Result.Failure<AttachmentDownload>(HelpdeskErrors.AttachmentNotFound());
        }

        var content = await store.OpenAsync(found.StoredName, cancellationToken).ConfigureAwait(false);

        if (content is null)
        {
            // The row is there and the bytes are not — a restore without the volume, or a
            // tidied directory. Not the caller's fault and not something they can fix, so
            // it is a 500 with its own code rather than a 404 that would say the attachment
            // never existed.
            return Result.Failure<AttachmentDownload>(HelpdeskErrors.AttachmentContentMissing());
        }

        return Result.Success(new AttachmentDownload(
            content,
            found.FileName,
            found.ContentType,
            found.ByteLength));
    }
}
