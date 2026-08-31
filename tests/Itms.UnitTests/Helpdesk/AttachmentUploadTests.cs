using System.Text;
using Itms.Modules.Helpdesk.Configuration;
using Itms.Modules.Helpdesk.Features.TicketAttachments.UploadTicketAttachment;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// The upload rules CONVENTIONS.md's security floor names — allowlist, size cap, content
/// sniffing — asserted without a web server, a database, or a signed-in caller.
/// </summary>
public sealed class AttachmentUploadTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly HelpdeskAttachmentOptions _options = new();

    [Fact]
    public void An_accepted_file_comes_back_with_the_type_the_server_chose()
    {
        var result = AttachmentUpload.Check(File("screenshot.png", 1024), _options);

        result.IsSuccess.ShouldBeTrue();
        result.Value.FileName.ShouldBe("screenshot.png");
        result.Value.Extension.ShouldBe(".png");
        result.Value.ContentType.ShouldBe("image/png");
    }

    /// <summary>
    /// The media type is a function of the extension, never of what the client declared —
    /// which is the whole reason <c>UploadedFile</c> carries no content type to be tempted by.
    /// </summary>
    [Fact]
    public void An_uppercase_extension_is_accepted_and_normalised()
    {
        var result = AttachmentUpload.Check(File("REPORT.PDF", 2048), _options);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Extension.ShouldBe(".pdf");
        result.Value.ContentType.ShouldBe("application/pdf");
    }

    [Fact]
    public void An_extension_off_the_allowlist_is_refused()
    {
        var result = AttachmentUpload.Check(File("payload.exe", 512), _options);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.attachment_type_not_allowed");
        result.Error.FieldErrors!.ShouldContainKey("file");
    }

    [Fact]
    public void A_file_with_no_extension_at_all_is_refused() =>
        AttachmentUpload.Check(File("README", 512), _options)
            .Error!.Code.ShouldBe("helpdesk.attachment_type_not_allowed");

    /// <summary>
    /// Configuration narrows the built-in set, so an extension the code knows but the
    /// deployment has switched off is refused like any other.
    /// </summary>
    [Fact]
    public void A_deployment_that_narrows_the_allowlist_refuses_what_it_dropped()
    {
        var narrowed = new HelpdeskAttachmentOptions { AllowedExtensions = [".png", ".jpg"] };

        AttachmentUpload.Check(File("archive.zip", 512), narrowed)
            .Error!.Code.ShouldBe("helpdesk.attachment_type_not_allowed");

        AttachmentUpload.Check(File("shot.png", 512), narrowed).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void A_file_over_the_cap_is_refused_before_a_byte_is_written()
    {
        var result = AttachmentUpload.Check(File("huge.zip", _options.MaxBytes + 1), _options);

        result.Error!.Code.ShouldBe("helpdesk.attachment_too_large");
    }

    /// <summary>Exactly at the cap is inside it, not over.</summary>
    [Fact]
    public void A_file_exactly_at_the_cap_is_accepted() =>
        AttachmentUpload.Check(File("exact.zip", _options.MaxBytes), _options)
            .IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void An_empty_upload_is_not_a_file(long length) =>
        AttachmentUpload.Check(File("empty.txt", length), _options)
            .Error!.Code.ShouldBe("helpdesk.attachment_file_required");

    /// <summary>
    /// The stored name is generated, so a traversal attempt cannot reach the filesystem —
    /// but the display name must not read like a path either, because a display string
    /// somebody eventually treats as one is how that stops being true.
    /// </summary>
    [Theory]
    [InlineData("../../../etc/passwd.txt", "passwd.txt")]
    [InlineData("..\\..\\windows\\system32\\config.txt", "config.txt")]
    [InlineData("/absolute/path/notes.txt", "notes.txt")]
    [InlineData("C:\\Users\\ada\\Desktop\\notes.txt", "notes.txt")]
    public void A_name_carrying_a_path_is_reduced_to_its_last_segment(string sent, string expected) =>
        AttachmentUpload.Check(File(sent, 100), _options).Value.FileName.ShouldBe(expected);

    /// <summary>
    /// Control characters would break the download's Content-Disposition header and any log
    /// line the name lands in.
    /// </summary>
    [Fact]
    public void Control_characters_are_stripped_from_the_name() =>
        AttachmentUpload.Check(File("re\r\nport\u0000.pdf", 100), _options)
            .Value.FileName.ShouldBe("report.pdf");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    public void A_name_with_nothing_left_after_cleaning_is_refused(string sent) =>
        AttachmentUpload.Check(File(sent, 100), _options)
            .Error!.Code.ShouldBe("helpdesk.attachment_file_required");

    [Fact]
    public async Task Content_matching_the_extension_passes()
    {
        var file = File("shot.png", 8, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        (await AttachmentUpload.CheckContentAsync(file, ".png", Token)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Content_that_contradicts_the_extension_is_refused()
    {
        var file = File("shot.png", 4, [0x4D, 0x5A, 0x90, 0x00]);

        var result = await AttachmentUpload.CheckContentAsync(file, ".png", Token);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.attachment_content_mismatch");
    }

    /// <summary>
    /// A file shorter than the sniff window is normal — a one-line log, a tiny CSV — and
    /// must be judged on the bytes it has rather than refused for being short.
    /// </summary>
    [Fact]
    public async Task A_file_shorter_than_the_sniff_window_is_judged_on_what_it_has()
    {
        var file = File("tiny.csv", 2, Encoding.UTF8.GetBytes("a\n"));

        (await AttachmentUpload.CheckContentAsync(file, ".csv", Token)).IsSuccess.ShouldBeTrue();
    }

    private static UploadedFile File(string name, long length, byte[]? content = null) =>
        new(name, length, () => new MemoryStream(content ?? []));
}
