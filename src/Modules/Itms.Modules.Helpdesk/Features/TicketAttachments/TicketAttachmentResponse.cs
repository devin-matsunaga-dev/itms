using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.TicketAttachments;

/// <summary>A file attached to a ticket, as the API describes it.</summary>
/// <remarks>
/// <para>
/// Metadata only. The bytes are fetched from
/// <c>GET /api/v1/tickets/{ticketId}/attachments/{id}</c>, which re-checks the ticket and
/// the audience before it opens anything — CONVENTIONS.md's "served through an authorized
/// endpoint that re-checks permission".
/// </para>
/// <para>
/// <b><c>StoredName</c> is deliberately absent.</b> It is the file's name on disk
/// and it is nobody's business outside the store: a client addresses an attachment by
/// <see cref="Id"/>, and the one thing an internal identifier would add is a hint about
/// where the bytes live.
/// </para>
/// </remarks>
/// <param name="Id">The attachment's id. What the download route names.</param>
/// <param name="TicketId">The ticket it hangs off.</param>
/// <param name="FileName">The name the uploader's file had.</param>
/// <param name="ContentType">The media type the download will declare.</param>
/// <param name="ByteLength">How large it is.</param>
/// <param name="IsInternal">True when the requester cannot see it. Always false in a payload they receive.</param>
/// <param name="UploadedById">Who uploaded it.</param>
/// <param name="UploadedByName">Their display name at the time.</param>
/// <param name="CreatedAt">When it was uploaded (UTC).</param>
public sealed record TicketAttachmentResponse(
    Guid Id,
    Guid TicketId,
    string FileName,
    string ContentType,
    long ByteLength,
    bool IsInternal,
    Guid UploadedById,
    string UploadedByName,
    DateTimeOffset CreatedAt)
{
    /// <summary>The projection every attachment query uses, so one shape is built in one place.</summary>
    internal static System.Linq.Expressions.Expression<Func<TicketAttachment, TicketAttachmentResponse>> Projection() =>
        attachment => new TicketAttachmentResponse(
            attachment.Id,
            attachment.TicketId,
            attachment.FileName,
            attachment.ContentType,
            attachment.ByteLength,
            attachment.IsInternal,
            attachment.UploadedById,
            attachment.UploadedByName,
            attachment.CreatedAt);
}
