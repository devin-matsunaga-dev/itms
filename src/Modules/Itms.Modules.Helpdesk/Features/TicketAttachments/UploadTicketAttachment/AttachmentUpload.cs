using Itms.Modules.Helpdesk.Configuration;
using Itms.Modules.Helpdesk.Domain;
using Itms.Platform.Results;

namespace Itms.Modules.Helpdesk.Features.TicketAttachments.UploadTicketAttachment;

/// <summary>
/// What a file has to satisfy before any of its bytes are written: a name, an accepted
/// extension, a size inside the cap, and contents that agree with the extension.
/// </summary>
/// <remarks>
/// <para>
/// Separated from the handler because these are the rules CONVENTIONS.md's security floor
/// names, and they are worth being able to state — and to test — without a database, a
/// ticket, or a signed-in caller. The handler decides <em>who</em> may upload; this decides
/// <em>what</em> may be uploaded.
/// </para>
/// <para>
/// Every refusal is a validation failure keyed on <c>file</c>, so a form maps all four back
/// onto the same field.
/// </para>
/// </remarks>
public static class AttachmentUpload
{
    /// <summary>
    /// Checks <paramref name="file"/>'s name, extension, and declared size.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The name is reduced to its last segment first.</b> <c>Path.GetFileName</c> strips
    /// anything a caller put in front of it, so <c>../../etc/passwd</c> becomes
    /// <c>passwd</c> — not because the result is ever used as a path (it is not; the store
    /// generates its own name) but because a display string that still reads like a path is
    /// a display string somebody will eventually treat as one.
    /// </para>
    /// <para>
    /// The extension is then taken from the reduced name and matched case-insensitively
    /// against the configured allowlist, which startup has already restricted to extensions
    /// <see cref="AttachmentContentRules"/> knows how to check.
    /// </para>
    /// </remarks>
    /// <param name="file">The uploaded file.</param>
    /// <param name="options">The configured cap and allowlist.</param>
    /// <returns>The accepted name and media type, or the failure that refused it.</returns>
    public static Result<AcceptedUpload> Check(UploadedFile file, HelpdeskAttachmentOptions options)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(options);

        if (file.Length <= 0)
        {
            return Result.Failure<AcceptedUpload>(HelpdeskErrors.AttachmentFileRequired());
        }

        if (file.Length > options.MaxBytes)
        {
            return Result.Failure<AcceptedUpload>(HelpdeskErrors.AttachmentTooLarge(options.MaxBytes));
        }

        var fileName = SafeName(file.FileName);

        if (fileName is null)
        {
            return Result.Failure<AcceptedUpload>(HelpdeskErrors.AttachmentFileRequired());
        }

        var extension = AttachmentContentRules.ExtensionOf(fileName);

        if (extension is null
            || !options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            || !AttachmentContentRules.TryContentTypeFor(extension, out var contentType))
        {
            return Result.Failure<AcceptedUpload>(
                HelpdeskErrors.AttachmentTypeNotAllowed(options.AllowedExtensions));
        }

        return Result.Success(new AcceptedUpload(fileName, extension, contentType));
    }

    /// <summary>
    /// Reads the first bytes of <paramref name="file"/> and checks they are what
    /// <paramref name="extension"/> claims.
    /// </summary>
    /// <remarks>
    /// The allowlist alone is a check on a string the uploader chose; this is the check that
    /// survives them renaming <c>payload.exe</c> to <c>screenshot.png</c>.
    /// </remarks>
    /// <param name="file">The uploaded file.</param>
    /// <param name="extension">The extension already found to be allowed.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Success, or the content-mismatch failure.</returns>
    public static async Task<Result> CheckContentAsync(
        UploadedFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        var buffer = new byte[AttachmentContentRules.SniffLength];
        int read;

        var stream = file.OpenReadStream();

        await using (stream.ConfigureAwait(false))
        {
            read = await stream.ReadAtLeastAsync(
                buffer,
                buffer.Length,
                throwOnEndOfStream: false,
                cancellationToken).ConfigureAwait(false);
        }

        return AttachmentContentRules.ContentMatches(extension, buffer.AsSpan(0, read))
            ? Result.Success()
            : Result.Failure(HelpdeskErrors.AttachmentContentMismatch());
    }

    /// <summary>
    /// The uploader's file name reduced to something safe to store and to echo back, or
    /// <see langword="null"/> when nothing usable is left.
    /// </summary>
    /// <remarks>
    /// Control characters go because they would corrupt the download's
    /// <c>Content-Disposition</c> header and any log line the name lands in. Directory
    /// separators are removed by <c>Path.GetFileName</c> for the host's own convention;
    /// both conventions are then stripped explicitly, because a name produced on Windows and
    /// uploaded on Linux keeps its backslashes.
    /// </remarks>
    private static string? SafeName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        var cleaned = new string([.. name.Where(character => !char.IsControl(character))]).Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }
}

/// <summary>A file that passed the name, extension, and size checks.</summary>
/// <param name="FileName">The uploader's name, reduced to its last path segment.</param>
/// <param name="Extension">Its lower-cased, dotted extension.</param>
/// <param name="ContentType">The media type derived from that extension — never the client's claim.</param>
public sealed record AcceptedUpload(string FileName, string Extension, string ContentType);
