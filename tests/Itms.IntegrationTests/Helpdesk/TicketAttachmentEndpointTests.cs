using System.Net;
using System.Text;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.AuditModule;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// The attachment routes over the wire: the upload rules CONVENTIONS.md's security floor
/// names, and the headers the download has to answer with.
/// </summary>
/// <remarks>
/// Who may see what has a suite of its own — <see cref="TicketInternalVisibilityTests"/>.
/// This file is about the file: what is accepted, what is refused, and what comes back.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketAttachmentEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_uploaded_file_comes_back_described_and_attributed()
    {
        var ticket = await TicketAsync();
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var attachment = await TicketThreadClient.AttachesAsync(technician, ticket, "screenshot.png", Token);

        attachment.TicketId.ShouldBe(ticket);
        attachment.FileName.ShouldBe("screenshot.png");
        attachment.ContentType.ShouldBe("image/png");
        attachment.ByteLength.ShouldBe(TicketThreadClient.Png.Length);
        attachment.IsInternal.ShouldBeFalse();
        attachment.UploadedById.ShouldBe(techId);
        attachment.UploadedByName.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>What went up is what comes back down.</summary>
    [Fact]
    public async Task The_bytes_come_back_unchanged()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var attachment = await TicketThreadClient.AttachesAsync(technician, ticket, "screenshot.png", Token);

        var response = await TicketThreadClient.DownloadAsync(technician, ticket, attachment.Id, Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await response.Content.ReadAsByteArrayAsync(Token)).ShouldBe(TicketThreadClient.Png);
    }

    /// <summary>
    /// The two headers that keep an uploaded file from becoming stored cross-site
    /// scripting: the browser must not render it inline, and must not second-guess the
    /// declared type.
    /// </summary>
    [Fact]
    public async Task The_download_is_always_an_attachment_and_never_sniffed()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        // A text file, because that is the kind a browser would most happily render.
        var attachment = await TicketThreadClient.AttachesAsync(
            technician, ticket, "notes.txt", Token, content: "<script>alert(1)</script>"u8.ToArray());

        var response = await TicketThreadClient.DownloadAsync(technician, ticket, attachment.Id, Token);

        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        response.Content.Headers.ContentDisposition.FileName!.ShouldContain("notes.txt");
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
    }

    /// <summary>
    /// The media type is decided from the validated extension, so a client claiming
    /// something else gains nothing by it.
    /// </summary>
    [Fact]
    public async Task The_declared_media_type_is_ignored_in_favour_of_the_extension()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var response = await TicketThreadClient.PostAttachmentAsync(
            technician,
            ticket,
            "notes.txt",
            "plain text"u8.ToArray(),
            Token,
            declaredContentType: "text/html");

        response.EnsureSuccessStatusCode();

        var attachment = await ApiClient.ReadAsync<TicketAttachmentDto>(response, Token);
        attachment.ContentType.ShouldBe("text/plain");

        var download = await TicketThreadClient.DownloadAsync(technician, ticket, attachment.Id, Token);
        download.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
    }

    [Fact]
    public async Task An_extension_off_the_allowlist_is_refused()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var response = await TicketThreadClient.PostAttachmentAsync(
            technician, ticket, "payload.exe", [0x4D, 0x5A, 0x90, 0x00], Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token))
            .Code.ShouldBe("helpdesk.attachment_type_not_allowed");
    }

    /// <summary>
    /// The allowlist alone is a check on a string the uploader chose. This is the check
    /// that survives them renaming the file to get past it.
    /// </summary>
    [Fact]
    public async Task An_executable_renamed_to_a_png_is_refused_on_its_contents()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var response = await TicketThreadClient.PostAttachmentAsync(
            technician,
            ticket,
            "screenshot.png",
            [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00],
            Token,
            declaredContentType: "image/png");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.attachment_content_mismatch");
        problem.Errors!.ShouldContainKey("file");
    }

    [Fact]
    public async Task An_empty_file_is_refused()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var response = await TicketThreadClient.PostAttachmentAsync(
            technician, ticket, "empty.png", [], Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token))
            .Code.ShouldBe("helpdesk.attachment_file_required");
    }

    /// <summary>
    /// The stored name is generated, so a path in the uploader's name cannot reach the
    /// filesystem — but it must not survive into the display name either.
    /// </summary>
    [Fact]
    public async Task A_file_name_carrying_a_path_is_reduced_before_it_is_stored()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var attachment = await TicketThreadClient.AttachesAsync(
            technician, ticket, "../../../etc/passwd.png", Token);

        attachment.FileName.ShouldBe("passwd.png");
    }

    /// <summary>
    /// A file over the cap never reaches the store. The declared length is what refuses it
    /// here; the store's own ceiling is unit-tested, because it exists for the case where
    /// the declaration was a lie.
    /// </summary>
    [Fact]
    public async Task A_file_over_the_cap_is_refused()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        // Just over 10 MB of text, which is a valid .txt as far as the sniffer is concerned.
        var oversized = Encoding.ASCII.GetBytes(new string('a', (10 * 1024 * 1024) + 1));

        var response = await TicketThreadClient.PostAttachmentAsync(
            technician, ticket, "huge.txt", oversized, Token);

        // 400 from the handler, or 413 from the server's own request limit if the whole
        // body was refused before binding. Either is a correct refusal with a problem
        // document; what must not happen is a stored file.
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge);

        (await TicketThreadClient.ListAttachmentsAsync(technician, ticket, Token)).Total.ShouldBe(0);
    }

    /// <summary>
    /// SPEC.md §14 gives a User their own tickets, and a screenshot is how most tickets
    /// become useful. Public only — the internal half is asserted in the visibility suite.
    /// </summary>
    [Fact]
    public async Task A_requester_may_attach_a_public_file_to_their_own_ticket()
    {
        var ticket = await OwnTicketAsync();
        var endUserId = await TicketClient.UserIdAsync(fixture, "user", Token);

        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var attachment = await TicketThreadClient.AttachesAsync(endUser, ticket, "error.png", Token);

        attachment.UploadedById.ShouldBe(endUserId);
        attachment.IsInternal.ShouldBeFalse();

        // And they can fetch back what they just uploaded.
        (await TicketThreadClient.DownloadAsync(endUser, ticket, attachment.Id, Token))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Uploading_to_somebody_elses_ticket_is_not_found()
    {
        var ticket = await TicketAsync();

        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var response = await TicketThreadClient.PostAttachmentAsync(
            endUser, ticket, "error.png", TicketThreadClient.Png, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_list_comes_back_newest_first()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        await TicketThreadClient.AttachesAsync(technician, ticket, "first.png", Token);
        await TicketThreadClient.AttachesAsync(technician, ticket, "second.png", Token);

        var page = await TicketThreadClient.ListAttachmentsAsync(technician, ticket, Token);

        page.Items.Select(attachment => attachment.FileName).ShouldBe(["second.png", "first.png"]);
    }

    [Fact]
    public async Task An_attachment_that_does_not_exist_is_not_found()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var response = await TicketThreadClient.DownloadAsync(technician, ticket, Guid.CreateVersion7(), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token))
            .Code.ShouldBe("helpdesk.attachment_not_found");
    }

    [Fact]
    public async Task An_upload_without_an_antiforgery_token_is_refused()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(TicketThreadClient.Png), "file", "screenshot.png");

        var response = await technician.PostAsync(
            new Uri($"/api/v1/tickets/{ticket}/attachments", UriKind.Relative), form, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("auth.antiforgery_failed");
    }

    [Fact]
    public async Task An_anonymous_caller_can_neither_list_upload_nor_download()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var attachment = await TicketThreadClient.AttachesAsync(technician, ticket, "screenshot.png", Token);

        using var anonymous = fixture.CreateClient();

        (await anonymous.GetAsync(new Uri($"/api/v1/tickets/{ticket}/attachments", UriKind.Relative), Token))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await TicketThreadClient.DownloadAsync(anonymous, ticket, attachment.Id, Token))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await TicketThreadClient.PostAttachmentAsync(
            anonymous, ticket, "screenshot.png", TicketThreadClient.Png, Token))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// SPEC.md §15 counts ticket modifications as mandatory coverage. Written through
    /// <c>IAuditWriter</c> inside the request, because ARCHITECTURE.md §5 names no
    /// attachment event.
    /// </summary>
    [Fact]
    public async Task Uploading_writes_an_audit_row_against_the_ticket()
    {
        var ticket = await TicketAsync();
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var attachment = await TicketThreadClient.AttachesAsync(technician, ticket, "screenshot.png", Token);

        var rows = await AuditQueries.ByEntityAsync(fixture.DataSource, "Ticket", ticket.ToString(), Token);
        var row = rows.Single(entry => entry.Action == "helpdesk.ticket_attachment_added");

        row.ActorId.ShouldBe(techId);
        row.SourceIp.ShouldBe(IdentityWebFixture.RemoteIpAddress);
        row.Changes["attachmentId"].After.ShouldBe(attachment.Id.ToString());
        row.Changes["fileName"].After.ShouldBe("screenshot.png");
        row.Changes["contentType"].After.ShouldBe("image/png");
        row.Changes["isInternal"].After.ShouldBe("False");
    }

    /// <summary>
    /// A refused upload must leave no row, no trail entry, and — because the bytes are
    /// written before the transaction — no file the ticket does not know about.
    /// </summary>
    [Fact]
    public async Task A_refused_upload_stores_nothing()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        (await TicketThreadClient.PostAttachmentAsync(
            technician, ticket, "payload.exe", [0x4D, 0x5A], Token))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await TicketThreadClient.ListAttachmentsAsync(technician, ticket, Token)).Total.ShouldBe(0);

        var rows = await AuditQueries.ByEntityAsync(fixture.DataSource, "Ticket", ticket.ToString(), Token);
        rows.ShouldNotContain(entry => entry.Action == "helpdesk.ticket_attachment_added");
    }

    /// <summary>A ticket raised by and for the technician: not the end user's.</summary>
    private async Task<Guid> TicketAsync()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var ticket = await TicketClient.CreateAsync(
            technician, reference, departmentId, "Laptop will not charge", Token);

        return ticket.Id;
    }

    /// <summary>The same ticket, raised on the end user's behalf so it is theirs.</summary>
    private async Task<Guid> OwnTicketAsync()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var endUserId = await TicketClient.UserIdAsync(fixture, "user", Token);

        var ticket = await TicketClient.CreateAsync(
            technician, reference, departmentId, "Laptop will not charge", Token, requesterId: endUserId);

        return ticket.Id;
    }
}
