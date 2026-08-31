using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Itms.Modules.Helpdesk.Features.TicketAttachments;

/// <summary>
/// What an uploaded file has to look like to be accepted: a known extension, and leading
/// bytes that agree with it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The client's own <c>Content-Type</c> is never read.</b> It is a claim, made by the
/// party being checked, and honouring it is how a file called <c>notes.txt</c> gets served
/// back as <c>text/html</c>. The type stored on the row and declared by the download comes
/// from <see cref="TryContentTypeFor"/>, which is a function of the extension this class
/// itself validated — so the type the system states is one the system decided.
/// </para>
/// <para>
/// <b>Hand-written rather than a library.</b> Twelve extensions and seven signatures is a
/// table, not a dependency, and it is the same call the repository made for
/// <c>CsvParser</c>. If the accepted set ever grows past what one screen of literals
/// describes — office formats by their internal manifest, media containers, anything
/// needing real format parsing — that is the point to take a maintained detector rather
/// than grow this.
/// </para>
/// <para>
/// <b>The three text kinds have no signature, and that is not a gap being ignored.</b>
/// Plain text has no magic number to check, so the check that is available is applied
/// instead: the prefix must decode as UTF-8 and must contain no NUL byte, which is what
/// separates a log file from a renamed executable. It does not prove the file is text all
/// the way down, and nothing short of reading all of it would; the download's
/// <c>Content-Disposition: attachment</c> and <c>X-Content-Type-Options: nosniff</c> are
/// what make being wrong harmless.
/// </para>
/// </remarks>
public static class AttachmentContentRules
{
    /// <summary>
    /// How many leading bytes are read to make the decision. Comfortably more than the
    /// longest signature; the rest is what gives the text check something to look at.
    /// </summary>
    public const int SniffLength = 512;

    /// <summary>
    /// The media type each accepted extension is served as. The key set is also the set of
    /// extensions configuration is allowed to name.
    /// </summary>
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.Ordinal)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".txt"] = "text/plain",
        [".log"] = "text/plain",
        [".csv"] = "text/csv",
        [".zip"] = "application/zip",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // Every JPEG variant — JFIF, Exif, raw — opens with the same start-of-image marker
    // followed by the first segment's own marker byte.
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    // "PK" followed by the local-file, end-of-central-directory, or spanning marker. docx
    // and xlsx are zip containers, so all three share this table — the difference between
    // them is inside the archive, and this system never opens one.
    private static readonly byte[][] ZipSignatures =
    [
        [0x50, 0x4B, 0x03, 0x04],
        [0x50, 0x4B, 0x05, 0x06],
        [0x50, 0x4B, 0x07, 0x08],
    ];

    /// <summary>Every extension this class knows how to check.</summary>
    public static IReadOnlyCollection<string> KnownExtensions => ContentTypes.Keys;

    /// <summary>
    /// The lower-cased, dotted extension of <paramref name="fileName"/>, or
    /// <see langword="null"/> when it has none.
    /// </summary>
    /// <param name="fileName">The uploader's file name.</param>
    public static string? ExtensionOf(string fileName)
    {
        var extension = Path.GetExtension(fileName);

        return string.IsNullOrEmpty(extension) ? null : extension.ToLowerInvariant();
    }

    /// <summary>
    /// The media type <paramref name="extension"/> is served as.
    /// </summary>
    /// <param name="extension">A lower-cased, dotted extension.</param>
    /// <param name="contentType">The media type, when the extension is one this class knows.</param>
    /// <returns><see langword="true"/> when the extension is known.</returns>
    public static bool TryContentTypeFor(string extension, [NotNullWhen(true)] out string? contentType) =>
        ContentTypes.TryGetValue(extension, out contentType);

    /// <summary>
    /// True when <paramref name="prefix"/> is consistent with <paramref name="extension"/>.
    /// </summary>
    /// <param name="extension">A lower-cased, dotted extension, already known to be allowed.</param>
    /// <param name="prefix">The file's first <see cref="SniffLength"/> bytes, or all of them if it is shorter.</param>
    public static bool ContentMatches(string extension, ReadOnlySpan<byte> prefix) => extension switch
    {
        ".pdf" => prefix.StartsWith("%PDF-"u8),
        ".png" => prefix.StartsWith(PngSignature),
        ".jpg" or ".jpeg" => prefix.StartsWith(JpegSignature),
        ".gif" => prefix.StartsWith("GIF87a"u8) || prefix.StartsWith("GIF89a"u8),
        // A RIFF container whose form type is WEBP. The four bytes between them are the
        // file length, which says nothing about the format.
        ".webp" => prefix.Length >= 12 && prefix.StartsWith("RIFF"u8) && prefix[8..12].SequenceEqual("WEBP"u8),
        ".zip" or ".docx" or ".xlsx" => IsZipContainer(prefix),
        ".txt" or ".log" or ".csv" => LooksLikeText(prefix),
        // Unreachable while startup rejects an extension with no rule, and a closed switch
        // is what keeps it unreachable: adding one to ContentTypes without adding it here
        // refuses every upload of that kind rather than accepting every upload of it.
        _ => false,
    };

    /// <summary>
    /// True when <paramref name="prefix"/> opens one of the three zip record headers.
    /// </summary>
    /// <remarks>
    /// A plain loop rather than <c>Array.Exists</c>: the predicate would have to close over
    /// a <see cref="ReadOnlySpan{T}"/>, and a ref struct cannot be captured.
    /// </remarks>
    private static bool IsZipContainer(ReadOnlySpan<byte> prefix)
    {
        foreach (var signature in ZipSignatures)
        {
            if (prefix.StartsWith(signature))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when <paramref name="prefix"/> could be the start of a text file: valid UTF-8,
    /// and free of the NUL bytes that binaries are full of and text never contains.
    /// </summary>
    /// <remarks>
    /// A UTF-8 BOM decodes cleanly, so a file saved by a Windows editor passes. A prefix
    /// cut mid-character would not, which is why the decoder is the non-throwing one and
    /// the check is on the bytes rather than on the decoded string.
    /// </remarks>
    private static bool LooksLikeText(ReadOnlySpan<byte> prefix)
    {
        if (prefix.Contains((byte)0))
        {
            return false;
        }

        // Truncate to the last whole character before deciding: SniffLength is an arbitrary
        // cut, and a multi-byte character straddling it is not a malformed file.
        var decoder = Encoding.UTF8.GetDecoder();
        var characters = new char[Encoding.UTF8.GetMaxCharCount(prefix.Length)];

        try
        {
            decoder.Fallback = DecoderFallback.ExceptionFallback;
            decoder.Convert(prefix, characters, flush: false, out _, out _, out _);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
