using System.Text;
using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.TicketAttachments;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// The store: generated opaque names, the hard size ceiling, and what happens when the
/// bytes are not where the row says they are.
/// </summary>
/// <remarks>
/// It writes to a temporary directory of its own and removes it afterwards. That is real
/// filesystem work in what CONVENTIONS.md calls a unit suite — but the thing under test
/// <em>is</em> the filesystem interaction, a few kilobytes through a temp directory costs
/// milliseconds, and faking it would leave the one behaviour that matters unasserted.
/// </remarks>
public sealed class FileSystemAttachmentStoreTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "itms-attachment-tests",
        Guid.CreateVersion7().ToString("N"));

    private readonly FileSystemAttachmentStore _store;

    public FileSystemAttachmentStoreTests() => _store = new FileSystemAttachmentStore(_root);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task What_goes_in_comes_back_out_byte_for_byte()
    {
        var content = Encoding.UTF8.GetBytes("the quick brown fox");

        var stored = await _store.SaveAsync(new MemoryStream(content), 1024, Token);

        stored.ByteLength.ShouldBe(content.Length);

        var read = await _store.OpenAsync(stored.StoredName, Token);
        read.ShouldNotBeNull();

        using var buffer = new MemoryStream();
        await using (read)
        {
            await read.CopyToAsync(buffer, Token);
        }

        buffer.ToArray().ShouldBe(content);
    }

    /// <summary>
    /// The name is generated and carries no extension, so a directory that somehow got
    /// exposed would still not serve an archive as an archive or a text file as markup.
    /// </summary>
    [Fact]
    public async Task The_stored_name_is_opaque_and_carries_no_extension()
    {
        var stored = await _store.SaveAsync(new MemoryStream([1, 2, 3]), 1024, Token);

        stored.StoredName.Length.ShouldBe(TicketAttachment.StoredNameLength);
        stored.StoredName.ShouldAllBe(character => char.IsAsciiHexDigitLower(character));
        Path.GetExtension(stored.StoredName).ShouldBeEmpty();
    }

    [Fact]
    public async Task Two_uploads_of_the_same_bytes_get_different_names()
    {
        var first = await _store.SaveAsync(new MemoryStream([1, 2, 3]), 1024, Token);
        var second = await _store.SaveAsync(new MemoryStream([1, 2, 3]), 1024, Token);

        second.StoredName.ShouldNotBe(first.StoredName);
    }

    /// <summary>
    /// The ceiling that does not depend on the caller having told the truth about its own
    /// content length. This is the one the declared-length check exists on top of.
    /// </summary>
    [Fact]
    public async Task A_stream_longer_than_the_ceiling_is_refused_mid_write() =>
        await Should.ThrowAsync<AttachmentTooLargeException>(
            _store.SaveAsync(new MemoryStream(new byte[2048]), 1024, Token));

    /// <summary>
    /// A refused write must leave nothing behind, or a caller could fill a disk by
    /// repeatedly sending something too large.
    /// </summary>
    [Fact]
    public async Task A_refused_write_leaves_no_partial_file()
    {
        await Should.ThrowAsync<AttachmentTooLargeException>(
            _store.SaveAsync(new MemoryStream(new byte[2048]), 1024, Token));

        Directory.Exists(_root).ShouldBeTrue();
        Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    /// <summary>Exactly at the ceiling is inside it.</summary>
    [Fact]
    public async Task A_stream_exactly_at_the_ceiling_is_written() =>
        (await _store.SaveAsync(new MemoryStream(new byte[1024]), 1024, Token))
            .ByteLength.ShouldBe(1024);

    /// <summary>
    /// A row whose file is gone — a restore without the volume — is a real possibility, so
    /// it is a null rather than an exception the download would have to catch.
    /// </summary>
    [Fact]
    public async Task Opening_a_name_with_no_file_behind_it_returns_nothing() =>
        (await _store.OpenAsync(Guid.CreateVersion7().ToString("N"), Token)).ShouldBeNull();

    [Fact]
    public async Task Deleting_removes_the_file_and_a_second_delete_is_silent()
    {
        var stored = await _store.SaveAsync(new MemoryStream([1, 2, 3]), 1024, Token);

        await _store.DeleteAsync(stored.StoredName, Token);
        (await _store.OpenAsync(stored.StoredName, Token)).ShouldBeNull();

        await _store.DeleteAsync(stored.StoredName, Token);
    }

    /// <summary>
    /// Nothing a user supplies becomes a path — but the name reaching a read comes from the
    /// database, and a defence that only holds while the database is intact is not a
    /// defence.
    /// </summary>
    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("not-hex-at-all")]
    [InlineData("ABCDEF01234567890ABCDEF012345678")]
    [InlineData("0123")]
    public async Task A_name_that_was_not_generated_here_is_refused_rather_than_resolved(string name) =>
        // The delegate overload, because argument validation throws synchronously from the
        // call rather than on the returned task — which is the framework's own convention.
        await Should.ThrowAsync<ArgumentException>(() => _store.OpenAsync(name, Token));
}
