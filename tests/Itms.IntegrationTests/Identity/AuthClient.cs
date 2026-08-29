using System.Net.Http.Json;
using System.Text.Json;
using Itms.Modules.Identity.Security;

namespace Itms.IntegrationTests.Identity;

/// <summary>What <c>/login</c> and <c>/me</c> return, as the suite reads it.</summary>
/// <param name="Id">The user id.</param>
/// <param name="UserName">The sign-in name.</param>
/// <param name="Email">The address.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Roles">The roles on the account.</param>
public sealed record MeResponse(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);

/// <summary>
/// The handful of request shapes every authentication test needs, written once.
/// </summary>
public static class AuthClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The seeded development password, so no test spells it out itself.</summary>
    public const string Password = "Dev!Passw0rd123";

    /// <summary>Signs in, fetching the antiforgery token first as a browser would.</summary>
    /// <param name="client">The client, which keeps the cookies.</param>
    /// <param name="userName">The sign-in name or email.</param>
    /// <param name="password">The password to try.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The raw response, so a test can assert on the status as well as the body.</returns>
    public static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { userName, password }, options: Json),
        };

        await AddAntiforgeryAsync(client, request, cancellationToken);
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>Posts to an endpoint that requires an antiforgery token.</summary>
    /// <param name="client">The client.</param>
    /// <param name="path">The path to post to.</param>
    /// <param name="body">The request body, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The raw response.</returns>
    public static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        await AddAntiforgeryAsync(client, request, cancellationToken);
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>Reads <c>/me</c>.</summary>
    /// <param name="client">The client.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The raw response.</returns>
    public static Task<HttpResponseMessage> MeAsync(HttpClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative), cancellationToken);
    }

    /// <summary>Deserialises a successful <c>/login</c> or <c>/me</c> body.</summary>
    /// <param name="response">The response to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The account described by the body.</returns>
    public static async Task<MeResponse> ReadUserAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        return await response.Content.ReadFromJsonAsync<MeResponse>(Json, cancellationToken)
            ?? throw new InvalidOperationException("The response body was empty.");
    }

    private static async Task AddAntiforgeryAsync(
        HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await client.GetFromJsonAsync<CsrfTokenResponse>(
            new Uri("/api/v1/auth/csrf", UriKind.Relative),
            Json,
            cancellationToken)
            ?? throw new InvalidOperationException("No antiforgery token was issued.");

        request.Headers.Add(token.HeaderName, token.Token);
    }
}
