namespace Itms.Modules.Helpdesk.Features.TicketAttachments;

/// <summary>What a successful write left behind.</summary>
/// <param name="StoredName">The generated, extensionless name the bytes are under.</param>
/// <param name="ByteLength">How many bytes were actually written.</param>
public sealed record StoredAttachment(string StoredName, long ByteLength);

/// <summary>
/// Where attachment bytes live. The database holds the row; this holds the file.
/// </summary>
/// <remarks>
/// <para>
/// An interface rather than a static helper because the bytes are the one part of this
/// module that is not transactional: the file is written before the row is inserted, and
/// something has to be able to take it away again when the insert does not commit. That is
/// easier to state — and to test — behind a seam.
/// </para>
/// <para>
/// It is deliberately unaware of tickets, comments, and permissions. It takes a stream and
/// gives back a name; every question about who may read that file is answered before
/// anything here is called.
/// </para>
/// </remarks>
public interface IAttachmentStore
{
    /// <summary>
    /// Writes <paramref name="content"/> under a newly generated name.
    /// </summary>
    /// <remarks>
    /// Stops and throws if the stream turns out to hold more than
    /// <paramref name="maxBytes"/>, having first removed the partial file. The caller has
    /// normally checked a declared length already; this is the check that does not depend
    /// on the caller having been told the truth.
    /// </remarks>
    /// <param name="content">The bytes to store, positioned at the beginning.</param>
    /// <param name="maxBytes">The hard ceiling. A stream longer than this is refused mid-write.</param>
    /// <param name="cancellationToken">Cancels the write and removes the partial file.</param>
    /// <returns>The generated name and the length written.</returns>
    /// <exception cref="AttachmentTooLargeException">The stream held more than <paramref name="maxBytes"/>.</exception>
    Task<StoredAttachment> SaveAsync(Stream content, long maxBytes, CancellationToken cancellationToken);

    /// <summary>
    /// Opens the stored bytes for reading, or returns <see langword="null"/> when the file
    /// is not there.
    /// </summary>
    /// <remarks>
    /// A missing file is a real possibility — a restore that brought the database back
    /// without the volume, an operator tidying a directory — so it is a return value rather
    /// than an exception, and the download turns it into a failure the caller can read.
    /// </remarks>
    /// <param name="storedName">The name from the attachment row.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>An open read stream, or <see langword="null"/>.</returns>
    Task<Stream?> OpenAsync(string storedName, CancellationToken cancellationToken);

    /// <summary>
    /// Removes stored bytes. Called only to clean up a file whose row was never committed.
    /// </summary>
    /// <remarks>
    /// There is no delete path for an attachment that exists: this is compensation for a
    /// failed write, not a feature. It is silent about a file that is already gone, because
    /// the only thing a caller does about that is nothing.
    /// </remarks>
    /// <param name="storedName">The name to remove.</param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    Task DeleteAsync(string storedName, CancellationToken cancellationToken);
}

/// <summary>
/// Thrown when a stream turns out to be longer than the ceiling it was given.
/// </summary>
/// <remarks>
/// One of the few places this module throws rather than returning a
/// <c>Result</c>: the discovery happens deep inside a copy loop, several frames below the
/// handler that knows how to describe a failure, and unwinding it any other way would mean
/// threading a result through the store's whole surface for a case that only arises when a
/// caller has misreported its own content length.
/// </remarks>
public sealed class AttachmentTooLargeException : Exception
{
    /// <summary>Creates the exception with the ceiling that was exceeded.</summary>
    /// <param name="maxBytes">The limit the stream went past.</param>
    public AttachmentTooLargeException(long maxBytes)
        : base($"The attachment is larger than the {maxBytes} byte limit.") => MaxBytes = maxBytes;

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public AttachmentTooLargeException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner exception.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public AttachmentTooLargeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception.</summary>
    public AttachmentTooLargeException()
    {
    }

    /// <summary>The ceiling that was exceeded, when one was given.</summary>
    public long MaxBytes { get; }
}
