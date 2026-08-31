using System.Globalization;
using Itms.Contracts.Auditing;
using Itms.Contracts.Lookups;
using Itms.Modules.Helpdesk.Auditing;
using Itms.Modules.Helpdesk.Configuration;
using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.Tickets;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Itms.Modules.Helpdesk.Features.TicketAttachments.UploadTicketAttachment;

/// <summary>
/// Attaches a file to a ticket: the row in the database, the bytes on disk.
/// </summary>
/// <remarks>
/// <para>
/// <b>Permission before content, always.</b> The ticket is checked against
/// <see cref="TicketScope"/> and the audience against <see cref="TicketVisibility"/> before
/// the file is so much as measured, so nothing about a ticket the caller may not touch can
/// be learned from how their upload was refused — and no work is done on their behalf until
/// they have been shown to be entitled to it.
/// </para>
/// <para>
/// <b>The bytes are written before the transaction, and removed if it does not commit.</b>
/// The alternative — holding the file open inside the transaction — would keep a database
/// transaction alive for the length of a ten-megabyte write over somebody's uplink. So the
/// two are ordered instead: a file with no row is unreachable garbage that a sweep can find
/// later, while a row with no file is a broken download that a user finds first. This is the
/// one place in the module where a failure can leave something behind, and it leaves behind
/// the harmless one.
/// </para>
/// <para>
/// <b>No domain event.</b> ARCHITECTURE.md §5 names none for an attachment and nothing
/// consumes one; the trail is written through §8's <c>IAuditWriter</c> beside the row. See
/// <c>HelpdeskAudit</c> for what WP-4.4 has to decide if it starts publishing.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is uploading.</param>
/// <param name="users">Identity's public contract, for the uploader's display name.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
/// <param name="store">Where the bytes go.</param>
/// <param name="options">The configured cap and allowlist.</param>
internal sealed class UploadTicketAttachmentHandler(
    HelpdeskDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IUserLookup users,
    IAuditWriter audit,
    IAttachmentStore store,
    IOptions<HelpdeskAttachmentOptions> options)
{
    /// <summary>Stores the file and records it against the ticket.</summary>
    /// <param name="ticketId">The ticket being attached to.</param>
    /// <param name="file">The uploaded file.</param>
    /// <param name="isInternal">Whether only the queue may see it.</param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>The stored attachment's metadata, or the failure that stopped it.</returns>
    public async Task<Result<TicketAttachmentResponse>> HandleAsync(
        Guid ticketId,
        UploadedFile file,
        bool isInternal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        var settings = options.Value;

        var visible = await database.Tickets
            .AsNoTracking()
            .VisibleTo(currentUser)
            .AnyAsync(ticket => ticket.Id == ticketId, cancellationToken)
            .ConfigureAwait(false);

        if (!visible)
        {
            return Result.Failure<TicketAttachmentResponse>(HelpdeskErrors.TicketNotFound());
        }

        if (isInternal && !TicketVisibility.SeesInternal(currentUser))
        {
            return Result.Failure<TicketAttachmentResponse>(HelpdeskErrors.InternalCommentForbidden());
        }

        var accepted = AttachmentUpload.Check(file, settings);

        if (accepted.IsFailure)
        {
            return Result.Failure<TicketAttachmentResponse>(accepted.Error!);
        }

        var content = await AttachmentUpload
            .CheckContentAsync(file, accepted.Value.Extension, cancellationToken)
            .ConfigureAwait(false);

        if (content.IsFailure)
        {
            return Result.Failure<TicketAttachmentResponse>(content.Error!);
        }

        var uploader = await TicketActor.ResolveAsync(currentUser, users, cancellationToken).ConfigureAwait(false);

        if (uploader is null)
        {
            return Result.Failure<TicketAttachmentResponse>(HelpdeskErrors.TicketNotFound());
        }

        StoredAttachment stored;

        try
        {
            var source = file.OpenReadStream();

            await using (source.ConfigureAwait(false))
            {
                stored = await store
                    .SaveAsync(source, settings.MaxBytes, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (AttachmentTooLargeException)
        {
            // The declared length was a lie, or the client streamed without one. The store
            // has already removed what it wrote.
            return Result.Failure<TicketAttachmentResponse>(
                HelpdeskErrors.AttachmentTooLarge(settings.MaxBytes));
        }

        var attachment = TicketAttachment.Attach(
            ticketId,
            accepted.Value.FileName,
            stored.StoredName,
            accepted.Value.ContentType,
            stored.ByteLength,
            isInternal,
            uploader.Id,
            uploader.Name,
            clock.UtcNow);

        try
        {
            await session.ExecuteInTransactionAsync(
                async token =>
                {
                    await session.EnlistAsync(database, token).ConfigureAwait(false);

                    database.TicketAttachments.Add(attachment);
                    await database.SaveChangesAsync(token).ConfigureAwait(false);

                    await audit.WriteAsync(
                        new AuditEntry(
                            HelpdeskAudit.TicketAttachmentAdded,
                            HelpdeskAudit.TicketEntityType,
                            ticketId.ToString(),
                            HelpdeskAudit.Changes()
                                .Set("attachmentId", attachment.Id.ToString())
                                .Set("fileName", attachment.FileName)
                                .Set("contentType", attachment.ContentType)
                                .Set("byteLength", attachment.ByteLength.ToString(CultureInfo.InvariantCulture))
                                .Set("isInternal", attachment.IsInternal.ToString(CultureInfo.InvariantCulture))),
                        token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Nothing committed, so nothing knows this file exists. Taking it away is the
            // difference between a rolled-back upload and a slow leak of orphaned bytes.
            // Deliberately not cancellable: a cancelled request is one of the ways to get
            // here, and cleanup that skips itself on cancellation cleans up least when it
            // is needed most.
            await store.DeleteAsync(stored.StoredName, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        return Result.Success(new TicketAttachmentResponse(
            attachment.Id,
            attachment.TicketId,
            attachment.FileName,
            attachment.ContentType,
            attachment.ByteLength,
            attachment.IsInternal,
            attachment.UploadedById,
            attachment.UploadedByName,
            attachment.CreatedAt));
    }
}
