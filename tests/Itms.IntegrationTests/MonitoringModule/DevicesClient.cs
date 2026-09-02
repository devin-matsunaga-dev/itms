using System.Net.Http.Json;
using System.Text.Json;
using Itms.IntegrationTests.Api;

namespace Itms.IntegrationTests.MonitoringModule;

/// <summary>A monitored device as the suite reads it off the wire.</summary>
/// <remarks>
/// <b>It has no field for the community string, deliberately.</b> Deserialisation would
/// silently ignore one, which is the opposite of what this suite needs to prove — so
/// <c>DeviceSnmpCredentialTests</c> asserts against the raw JSON instead, and this shape is
/// what everything else reads.
/// </remarks>
/// <param name="Id">The device's id.</param>
/// <param name="AssetId">The asset it is.</param>
/// <param name="AssetTag">That asset's tag.</param>
/// <param name="Hostname">The name it answers to.</param>
/// <param name="IpAddress">The address it is polled at.</param>
/// <param name="MonitoringEnabled">Whether the poller picks it up.</param>
/// <param name="PollIntervalSeconds">How often it is checked.</param>
/// <param name="FailureThreshold">How many consecutive failures declare it offline.</param>
/// <param name="SnmpEnabled">Whether the read-only SNMP checks apply.</param>
/// <param name="SnmpPort">The port those checks use.</param>
/// <param name="SnmpCredentialSet">Whether a community string is configured.</param>
/// <param name="CreatedAt">When the device was registered.</param>
/// <param name="UpdatedAt">When it last changed.</param>
public sealed record DeviceDto(
    Guid Id,
    Guid AssetId,
    string AssetTag,
    string? Hostname,
    string? IpAddress,
    bool MonitoringEnabled,
    int PollIntervalSeconds,
    int FailureThreshold,
    bool SnmpEnabled,
    int SnmpPort,
    bool SnmpCredentialSet,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>The monitored-device request shapes the suite needs, written once.</summary>
public static class DevicesClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The route the device endpoints hang off.</summary>
    public const string Devices = "/api/v1/devices";

    /// <summary>Posts a device and returns the raw response, so a test can assert the status.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="body">The request body.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The raw response.</returns>
    public static Task<HttpResponseMessage> PostDeviceAsync(
        HttpClient client,
        object body,
        CancellationToken cancellationToken) =>
        ApiClient.SendAsync(client, HttpMethod.Post, Devices, body, cancellationToken);

    /// <summary>Registers a device and returns it, failing loudly if the call did not succeed.</summary>
    /// <param name="client">An admin client.</param>
    /// <param name="assetId">The asset the device is.</param>
    /// <param name="hostname">The name it answers to.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The registered device.</returns>
    public static async Task<DeviceDto> RegisterAsync(
        HttpClient client,
        Guid assetId,
        string hostname,
        CancellationToken cancellationToken)
    {
        var response = await PostDeviceAsync(client, new { assetId, hostname }, cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ApiClient.ReadAsync<DeviceDto>(response, cancellationToken);
    }

    /// <summary>Reads one device, failing loudly if the call did not succeed.</summary>
    /// <param name="client">Any client that may read devices.</param>
    /// <param name="deviceId">The device to read.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The device.</returns>
    public static async Task<DeviceDto> GetAsync(
        HttpClient client,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var response = await client.GetAsync(new Uri($"{Devices}/{deviceId}", UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ApiClient.ReadAsync<DeviceDto>(response, cancellationToken);
    }

    /// <summary>The <c>ETag</c> a device read answers with.</summary>
    /// <param name="client">Any client that may read devices.</param>
    /// <param name="deviceId">The device to read.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The entity tag, as sent.</returns>
    public static async Task<string> ETagAsync(
        HttpClient client,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var response = await client.GetAsync(new Uri($"{Devices}/{deviceId}", UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();

        return response.Headers.ETag?.ToString()
            ?? throw new InvalidOperationException("The device read carried no ETag.");
    }

    /// <summary>
    /// Sends a request to a device route, optionally stating an <c>If-Match</c>
    /// precondition.
    /// </summary>
    /// <remarks>
    /// Built by hand rather than through <c>ApiClient.SendAsync</c> for the reason
    /// <c>AssetsClient.PutAssetAsync</c> is: that helper has nowhere to put a header, and a
    /// test has to be able to attach a deliberately stale or malformed tag.
    /// </remarks>
    /// <param name="client">A signed-in client.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path to call.</param>
    /// <param name="body">The request body, or null for none.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="ifMatch">The entity tag to require, or null to state no precondition.</param>
    /// <returns>The raw response.</returns>
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken,
        string? ifMatch = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        if (ifMatch is not null)
        {
            // Added without validation, so a test can send a deliberately malformed tag.
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        var csrf = await client.GetFromJsonAsync<CsrfDto>(
            new Uri("/api/v1/auth/csrf", UriKind.Relative),
            Json,
            cancellationToken)
            ?? throw new InvalidOperationException("No antiforgery token was issued.");

        request.Headers.Add(csrf.HeaderName, csrf.Token);
        return await client.SendAsync(request, cancellationToken);
    }

    private sealed record CsrfDto(string HeaderName, string Token);
}
