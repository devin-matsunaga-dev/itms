using System.Globalization;

namespace Itms.Modules.Helpdesk.Features.TicketAttachments;

/// <summary>
/// Attachment bytes on the local filesystem, under the configured root.
/// </summary>
/// <remarks>
/// <para>
/// <b>Layout.</b> <c>&lt;root&gt;/&lt;first two hex characters&gt;/&lt;stored name&gt;</c>.
/// The fan-out exists because a single directory holding every attachment a helpdesk ever
/// received is slow to list and unpleasant to operate; two characters is 256 buckets,
/// which is enough at this scale and costs one <c>mkdir</c>.
/// </para>
/// <para>
/// <b>Nothing the uploader supplied reaches a path.</b> The stored name is generated here
/// from a version 7 GUID and is the only thing that ever becomes a path segment, so there
/// is no traversal to defend against — and <see cref="PathFor"/> nevertheless refuses a
/// name that is not thirty-two hexadecimal characters, because the name reaching a read
/// comes from the database and a defence that only holds while the database is intact is
/// not a defence.
/// </para>
/// <para>
/// <b>It takes an already-resolved absolute root</b> rather than the options and the host
/// environment it would take to work one out. Resolving a configured relative path against
/// the content root is the composition's job, done once in <c>HelpdeskModule</c>; what is
/// left here is a store that can be pointed at a directory and exercised without a host.
/// </para>
/// <para>
/// Registered as a singleton: it holds a path and no per-request state.
/// </para>
/// </remarks>
/// <param name="rootPath">The absolute directory the bytes live under.</param>
public sealed class FileSystemAttachmentStore(string rootPath) : IAttachmentStore
{
    /// <summary>How many characters of the stored name name the bucket directory.</summary>
    private const int BucketLength = 2;

    /// <summary>Copy buffer. Large enough that a 10 MB file is a few hundred writes.</summary>
    private const int CopyBufferSize = 64 * 1024;

    private readonly string _root = Path.GetFullPath(rootPath);

    /// <inheritdoc />
    public async Task<StoredAttachment> SaveAsync(
        Stream content,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        var storedName = Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture);
        var path = PathFor(storedName);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            // FileMode.CreateNew, never Create: a generated name that already existed would
            // mean two attachments colliding, and silently overwriting one of them is the
            // worst of the available outcomes.
            var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                CopyBufferSize,
                useAsync: true);

            long written;

            await using (stream.ConfigureAwait(false))
            {
                written = await CopyCappedAsync(content, stream, maxBytes, cancellationToken).ConfigureAwait(false);
            }

            return new StoredAttachment(storedName, written);
        }
        catch
        {
            // Whatever went wrong — over the cap, cancelled, a full disk — the partial file
            // is not something anybody can use and nothing else knows it exists.
            Delete(path);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<Stream?> OpenAsync(string storedName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = PathFor(storedName);

        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string storedName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Delete(PathFor(storedName));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Copies <paramref name="source"/> into <paramref name="destination"/>, refusing to
    /// write more than <paramref name="maxBytes"/>.
    /// </summary>
    /// <remarks>
    /// The cap is checked after each read rather than from a declared length, because a
    /// declared length is something the caller stated. This is the check that holds when
    /// the statement was false.
    /// </remarks>
    private static async Task<long> CopyCappedAsync(
        Stream source,
        Stream destination,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[CopyBufferSize];
        long total = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                return total;
            }

            total += read;

            if (total > maxBytes)
            {
                throw new AttachmentTooLargeException(maxBytes);
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Cleaning up is best effort. An orphaned file is bytes nobody can reach, and
            // failing the request over it would replace an invisible problem with a visible
            // one for the person who did nothing wrong.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The full path a stored name maps to.</summary>
    /// <exception cref="ArgumentException"><paramref name="storedName"/> is not a generated name.</exception>
    private string PathFor(string storedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storedName);

        if (!IsGeneratedName(storedName))
        {
            throw new ArgumentException(
                "An attachment's stored name must be thirty-two hexadecimal characters.",
                nameof(storedName));
        }

        return Path.Combine(_root, storedName[..BucketLength], storedName);
    }

    private static bool IsGeneratedName(string storedName)
    {
        if (storedName.Length != Domain.TicketAttachment.StoredNameLength)
        {
            return false;
        }

        foreach (var character in storedName)
        {
            if (!char.IsAsciiHexDigitLower(character))
            {
                return false;
            }
        }

        return true;
    }
}
