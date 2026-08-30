using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Itms.IntegrationTests.DirectoryModule;

/// <summary>A department as the suite reads it off the wire.</summary>
/// <param name="Id">The department's id.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Code">Its short code.</param>
/// <param name="Description">Free text.</param>
/// <param name="IsActive">False once retired.</param>
public sealed record DepartmentDto(Guid Id, string Name, string? Code, string? Description, bool IsActive);

/// <summary>A location as the suite reads it off the wire.</summary>
/// <param name="Id">The node's id.</param>
/// <param name="Name">Its own name.</param>
/// <param name="Kind">Its level of the hierarchy.</param>
/// <param name="ParentId">Its parent, or null at the root.</param>
/// <param name="Path">The full display path.</param>
/// <param name="Depth">How far below the root it sits.</param>
/// <param name="ChildCount">How many nodes sit directly beneath it.</param>
public sealed record LocationDto(
    Guid Id,
    string Name,
    string Kind,
    Guid? ParentId,
    string Path,
    int Depth,
    int ChildCount);

/// <summary>The list envelope ARCHITECTURE.md §6 fixes.</summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items on this page.</param>
/// <param name="Total">The total across all pages.</param>
public sealed record PageDto<T>(IReadOnlyList<T> Items, int Total);

/// <summary>The problem document an error comes back as.</summary>
/// <param name="Title">The problem title.</param>
/// <param name="Detail">The human-readable message.</param>
/// <param name="Status">The HTTP status.</param>
/// <param name="Code">The stable machine-readable error code.</param>
public sealed record ProblemDto(string? Title, string? Detail, int? Status, [property: JsonPropertyName("code")] string? Code);

/// <summary>
/// The directory request shapes the suite needs, written once.
/// </summary>
/// <remarks>
/// Every unsafe verb fetches an antiforgery token first, exactly as a browser would.
/// A test that forgot would fail with a 400 about CSRF and say nothing about the
/// behaviour it meant to assert.
/// </remarks>
public static class DirectoryClient
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

    /// <summary>Creates a department and returns it, failing loudly if the call did not succeed.</summary>
    /// <param name="client">The admin client.</param>
    /// <param name="name">The department name.</param>
    /// <param name="code">The department code, or null.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The created department.</returns>
    public static async Task<DepartmentDto> CreateDepartmentAsync(
        HttpClient client,
        string name,
        string? code,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/v1/departments",
            new { name, code, description = (string?)null },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadAsync<DepartmentDto>(response, cancellationToken);
    }

    /// <summary>Creates a location and returns it, failing loudly if the call did not succeed.</summary>
    /// <param name="client">The admin client.</param>
    /// <param name="name">The node's name.</param>
    /// <param name="kind">The node's kind.</param>
    /// <param name="parentId">The parent, or null for a root.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The created location.</returns>
    public static async Task<LocationDto> CreateLocationAsync(
        HttpClient client,
        string name,
        string kind,
        Guid? parentId,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/v1/locations",
            new { name, kind, parentId, description = (string?)null },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadAsync<LocationDto>(response, cancellationToken);
    }

    private sealed record CsrfDto(string HeaderName, string Token);
}
