using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Itms.IntegrationTests.Api;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>One comment as the suite reads it off the wire.</summary>
/// <param name="Id">The comment's id.</param>
/// <param name="TicketId">The ticket it belongs to.</param>
/// <param name="Body">What was said.</param>
/// <param name="IsInternal">True when it is invisible to the requester.</param>
/// <param name="AuthorId">Who wrote it.</param>
/// <param name="AuthorName">Their cached display name.</param>
/// <param name="CreatedAt">When it was posted.</param>
public sealed record TicketCommentDto(
    Guid Id,
    Guid TicketId,
    string Body,
    bool IsInternal,
    Guid AuthorId,
    string AuthorName,
    DateTimeOffset CreatedAt);

/// <summary>One attachment's metadata as the suite reads it off the wire.</summary>
/// <param name="Id">The attachment's id.</param>
/// <param name="TicketId">The ticket it hangs off.</param>
/// <param name="FileName">The uploader's file name.</param>
/// <param name="ContentType">The media type the download declares.</param>
/// <param name="ByteLength">How large it is.</param>
/// <param name="IsInternal">True when it is invisible to the requester.</param>
/// <param name="UploadedById">Who uploaded it.</param>
/// <param name="UploadedByName">Their cached display name.</param>
/// <param name="CreatedAt">When it was uploaded.</param>
public sealed record TicketAttachmentDto(
    Guid Id,
    Guid TicketId,
    string FileName,
    string ContentType,
    long ByteLength,
    bool IsInternal,
    Guid UploadedById,
    string UploadedByName,
    DateTimeOffset CreatedAt);

/// <summary>
/// The comment and attachment routes, as a test drives them.
/// </summary>
/// <remarks>
/// The upload builds its own multipart request rather than going through
/// <see cref="ApiClient.SendAsync"/>, which takes an object and serialises it as JSON.
/// Everything else about it is the same: an antiforgery token is fetched first, exactly as
/// a browser would.
/// </remarks>
public static class TicketThreadClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>A one-pixel PNG. The smallest thing that is genuinely a PNG.</summary>
    public static byte[] Png { get; } =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89,
    ];

    /// <summary>Posts a comment and hands back the raw response.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="ticketId">The ticket being commented on.</param>
    /// <param name="body">What is being said.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="isInternal">Whether to ask for an internal note.</param>
    /// <returns>The raw response.</returns>
    public static Task<HttpResponseMessage> PostCommentAsync(
        HttpClient client,
        Guid ticketId,
        string body,
        CancellationToken cancellationToken,
        bool isInternal = false) =>
        ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/tickets/{ticketId}/comments",
            new { body, isInternal },
            cancellationToken);

    /// <summary>Posts a comment and reads it back, failing loudly if the call was refused.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="ticketId">The ticket being commented on.</param>
    /// <param name="body">What is being said.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="isInternal">Whether to ask for an internal note.</param>
    /// <returns>The posted comment.</returns>
    public static async Task<TicketCommentDto> CommentsAsync(
        HttpClient client,
        Guid ticketId,
        string body,
        CancellationToken cancellationToken,
        bool isInternal = false)
    {
        var response = await PostCommentAsync(client, ticketId, body, cancellationToken, isInternal);
        response.EnsureSuccessStatusCode();

        return await ApiClient.ReadAsync<TicketCommentDto>(response, cancellationToken);
    }

    /// <summary>Reads a page of a ticket's comments.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The page envelope.</returns>
    public static Task<PageDto<TicketCommentDto>> ListCommentsAsync(
        HttpClient client,
        Guid ticketId,
        CancellationToken cancellationToken) =>
        ApiClient.ListAsync<TicketCommentDto>(client, $"/api/v1/tickets/{ticketId}/comments", cancellationToken);

    /// <summary>Uploads a file and hands back the raw response.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="ticketId">The ticket being attached to.</param>
    /// <param name="fileName">The name to send the file under.</param>
    /// <param name="content">The bytes.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="isInternal">Whether to ask for an internal attachment.</param>
    /// <param name="declaredContentType">
    /// What the client claims the file is. Deliberately settable and deliberately ignored by
    /// the server — a test asserts that claiming <c>image/png</c> over text does not help.
    /// </param>
    /// <returns>The raw response.</returns>
    public static async Task<HttpResponseMessage> PostAttachmentAsync(
        HttpClient client,
        Guid ticketId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken,
        bool isInternal = false,
        string declaredContentType = "application/octet-stream")
    {
        ArgumentNullException.ThrowIfNull(client);

        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue(declaredContentType);
        form.Add(part, "file", fileName);
        form.Add(new StringContent(isInternal ? "true" : "false"), "isInternal");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"/api/v1/tickets/{ticketId}/attachments", UriKind.Relative))
        {
            Content = form,
        };

        var token = await client.GetFromJsonAsync<CsrfDto>(
            new Uri("/api/v1/auth/csrf", UriKind.Relative),
            Json,
            cancellationToken)
            ?? throw new InvalidOperationException("No antiforgery token was issued.");

        request.Headers.Add(token.HeaderName, token.Token);

        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>Uploads a file and reads its metadata back, failing loudly if it was refused.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="ticketId">The ticket being attached to.</param>
    /// <param name="fileName">The name to send the file under.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="isInternal">Whether to ask for an internal attachment.</param>
    /// <param name="content">The bytes, or a valid PNG by default.</param>
    /// <returns>The stored attachment's metadata.</returns>
    public static async Task<TicketAttachmentDto> AttachesAsync(
        HttpClient client,
        Guid ticketId,
        string fileName,
        CancellationToken cancellationToken,
        bool isInternal = false,
        byte[]? content = null)
    {
        var response = await PostAttachmentAsync(
            client, ticketId, fileName, content ?? Png, cancellationToken, isInternal);

        response.EnsureSuccessStatusCode();

        return await ApiClient.ReadAsync<TicketAttachmentDto>(response, cancellationToken);
    }

    /// <summary>Reads a page of a ticket's attachments.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The page envelope.</returns>
    public static Task<PageDto<TicketAttachmentDto>> ListAttachmentsAsync(
        HttpClient client,
        Guid ticketId,
        CancellationToken cancellationToken) =>
        ApiClient.ListAsync<TicketAttachmentDto>(
            client, $"/api/v1/tickets/{ticketId}/attachments", cancellationToken);

    /// <summary>Fetches an attachment's bytes.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="ticketId">The ticket the attachment must belong to.</param>
    /// <param name="attachmentId">The attachment.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The raw response, so a test can assert on a refusal or on the headers.</returns>
    public static Task<HttpResponseMessage> DownloadAsync(
        HttpClient client,
        Guid ticketId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        return client.GetAsync(
            new Uri($"/api/v1/tickets/{ticketId}/attachments/{attachmentId}", UriKind.Relative),
            cancellationToken);
    }

    private sealed record CsrfDto(string HeaderName, string Token);
}
