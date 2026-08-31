namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// A file somebody attached to a ticket: the row that describes it, never the bytes
/// themselves.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bytes live outside the database and outside the web root</b>, under a name this
/// row generates and nobody chooses (CONVENTIONS.md's security floor). The uploader's own
/// file name is kept in <see cref="FileName"/> for display and for the download's
/// <c>Content-Disposition</c>, and it is never used to build a path — that is the whole
/// reason <see cref="StoredName"/> exists as a separate column.
/// </para>
/// <para>
/// <b><see cref="StoredName"/> carries no extension.</b> An opaque name on disk means a
/// misconfigured server that ever exposed the storage directory would still not serve a
/// <c>.zip</c> as an archive or an <c>.html</c> as markup, and it removes any question of
/// what a scanner or an unpacker might do with it. The type the download declares comes
/// from <see cref="ContentType"/>, which the upload derived from the validated extension
/// rather than from anything the client said.
/// </para>
/// <para>
/// <b><see cref="IsInternal"/> means exactly what it means on a comment.</b> A note
/// explaining a workaround is worthless if the screenshot beside it is public, so an
/// attachment carries the same flag and is filtered by the same rule — see
/// <c>TicketVisibility</c>. It defaults to public, and only a Technician or an Admin may
/// set it.
/// </para>
/// <para>
/// <b>Write-once, like a comment.</b> No mutators, no delete path, no re-upload. The row
/// and the file it names are created together in one transaction and are never edited.
/// </para>
/// </remarks>
public sealed class TicketAttachment
{
    /// <summary>
    /// The longest an uploader-supplied file name may be, after the path has been stripped.
    /// </summary>
    /// <remarks>
    /// 255 is the file-name limit of every filesystem this deploys on. Nothing is written
    /// under this name, but the number is the one a person's tooling produced it under, so
    /// there is no reason to accept more.
    /// </remarks>
    public const int FileNameMaxLength = 255;

    /// <summary>The length of a generated storage name: a version 7 GUID in hexadecimal.</summary>
    public const int StoredNameLength = 32;

    /// <summary>The longest a content type may be.</summary>
    public const int ContentTypeMaxLength = 128;

    /// <summary>The longest a cached uploader name may be.</summary>
    public const int UploadedByNameMaxLength = Ticket.DisplayNameMaxLength;

    private TicketAttachment()
    {
        // EF Core materialisation; all four are non-null in the database.
        FileName = null!;
        StoredName = null!;
        ContentType = null!;
        UploadedByName = null!;
    }

    /// <summary>The attachment's id. What the download route names.</summary>
    public Guid Id { get; private set; }

    /// <summary>The ticket it hangs off. A real intra-module foreign key.</summary>
    public Guid TicketId { get; private set; }

    /// <summary>
    /// The name the uploader's file had. Display only — it never reaches the filesystem.
    /// </summary>
    public string FileName { get; private set; }

    /// <summary>
    /// The generated, extensionless name the bytes are stored under. Unique across the
    /// system, so the store never has to consult the ticket to find a file.
    /// </summary>
    public string StoredName { get; private set; }

    /// <summary>
    /// The media type the download declares, derived from the validated extension and
    /// never from the client's own <c>Content-Type</c> header.
    /// </summary>
    public string ContentType { get; private set; }

    /// <summary>How many bytes were actually written.</summary>
    public long ByteLength { get; private set; }

    /// <summary>True when only a Technician or an Admin may see or fetch it.</summary>
    public bool IsInternal { get; private set; }

    /// <summary>Who uploaded it.</summary>
    public Guid UploadedById { get; private set; }

    /// <summary>Their display name at the time. Cached, like every other name on a ticket.</summary>
    public string UploadedByName { get; private set; }

    /// <summary>When it was uploaded (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Records an uploaded file. The only way an attachment comes into existence.</summary>
    /// <param name="ticketId">The ticket it belongs to.</param>
    /// <param name="fileName">The uploader's file name, already stripped of any path.</param>
    /// <param name="storedName">The generated name the bytes were written under.</param>
    /// <param name="contentType">The media type derived from the validated extension.</param>
    /// <param name="byteLength">How many bytes were written.</param>
    /// <param name="isInternal">Whether the requester is excluded from seeing it.</param>
    /// <param name="uploadedById">Who uploaded it.</param>
    /// <param name="uploadedByName">Their display name, cached onto the row.</param>
    /// <param name="uploadedAt">When (UTC), from <c>IClock</c>.</param>
    /// <returns>The new attachment row, not yet persisted.</returns>
    /// <exception cref="ArgumentException">An identifier is empty or a required string is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="byteLength"/> is not positive.</exception>
    public static TicketAttachment Attach(
        Guid ticketId,
        string fileName,
        string storedName,
        string contentType,
        long byteLength,
        bool isInternal,
        Guid uploadedById,
        string uploadedByName,
        DateTimeOffset uploadedAt)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("An attachment must belong to a ticket.", nameof(ticketId));
        }

        if (uploadedById == Guid.Empty)
        {
            throw new ArgumentException("An attachment must have an uploader.", nameof(uploadedById));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedByName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLength);

        return new TicketAttachment
        {
            Id = Guid.CreateVersion7(),
            TicketId = ticketId,
            FileName = Truncate(fileName, FileNameMaxLength),
            StoredName = storedName,
            ContentType = Truncate(contentType, ContentTypeMaxLength),
            ByteLength = byteLength,
            IsInternal = isInternal,
            UploadedById = uploadedById,
            UploadedByName = Truncate(uploadedByName, UploadedByNameMaxLength),
            CreatedAt = uploadedAt,
        };
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length > maxLength ? value[..maxLength] : value;
}
