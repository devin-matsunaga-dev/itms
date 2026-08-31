namespace Itms.Modules.Helpdesk.Features.TicketAttachments.UploadTicketAttachment;

/// <summary>
/// The uploaded file as the handler sees it: a name, a declared length, and a way to read
/// the bytes more than once.
/// </summary>
/// <remarks>
/// <para>
/// A shape of this module's own rather than <c>IFormFile</c> directly, so the upload rules
/// — the allowlist, the cap, the signature check — can be exercised without a web server.
/// The endpoint adapts the framework's type onto it in one line.
/// </para>
/// <para>
/// <b><see cref="Length"/> is what the caller declared</b>, which is why it is not the only
/// place the cap is enforced: it is checked here to refuse an oversized upload before a
/// byte is written, and checked again inside the store while the bytes go past. The first
/// is a courtesy to an honest client; the second is the one that holds.
/// </para>
/// <para>
/// <b><see cref="OpenReadStream"/> is opened twice</b> — once to read the prefix the
/// signature check needs, once to copy. Two opens rather than a rewind, because the
/// contract of a stream handed over by a form reader does not promise seekability, and
/// re-opening is what <c>IFormFile</c> is designed for.
/// </para>
/// </remarks>
/// <param name="FileName">The name the uploader's file had, before any path is stripped.</param>
/// <param name="Length">The length the caller declared.</param>
/// <param name="OpenReadStream">Opens a fresh read stream over the content.</param>
public sealed record UploadedFile(string FileName, long Length, Func<Stream> OpenReadStream);
