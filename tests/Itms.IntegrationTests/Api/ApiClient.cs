using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Itms.IntegrationTests.Api;

/// <summary>The list envelope ARCHITECTURE.md §6 fixes.</summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items on this page.</param>
/// <param name="Total">The total across all pages.</param>
/// <param name="Page">The 1-based page number.</param>
/// <param name="PageSize">The applied page size.</param>
public sealed record PageDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>The problem document an error comes back as.</summary>
/// <param name="Title">The problem title.</param>
/// <param name="Detail">The human-readable message.</param>
/// <param name="Status">The HTTP status.</param>
/// <param name="Code">The stable machine-readable error code.</param>
/// <param name="Errors">Per-field validation messages, when there are any.</param>
public sealed record ProblemDto(
    string? Title,
    string? Detail,
    int? Status,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("errors")] IReadOnlyDictionary<string, string[]>? Errors);

/// <summary>
/// The HTTP plumbing every module's endpoint suite needs: a request with an antiforgery
/// token, and a JSON read.
/// </summary>
/// <remarks>
/// Every unsafe verb fetches an antiforgery token first, exactly as a browser would. A
/// test that forgot would fail with a 400 about CSRF and say nothing about the behaviour
/// it meant to assert.
/// </remarks>
public static class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Sends a request with a body and an antiforgery token.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path to call.</param>
    /// <param name="body">The request body, or null for none.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The raw response.</returns>
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        var token = await client.GetFromJsonAsync<CsrfDto>(
            new Uri("/api/v1/auth/csrf", UriKind.Relative),
            Json,
            cancellationToken)
            ?? throw new InvalidOperationException("No antiforgery token was issued.");

        request.Headers.Add(token.HeaderName, token.Token);
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>Reads a JSON body.</summary>
    /// <typeparam name="T">The shape to read.</typeparam>
    /// <param name="response">The response to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The deserialised body.</returns>
    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
            ?? throw new InvalidOperationException("The response body was empty.");
    }

    /// <summary>Reads a page and fails loudly if the call did not succeed.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="client">The signed-in client.</param>
    /// <param name="path">The list path, query string included.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The page envelope.</returns>
    public static async Task<PageDto<T>> ListAsync<T>(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var response = await client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<PageDto<T>>(response, cancellationToken);
    }

    private sealed record CsrfDto(string HeaderName, string Token);
}
