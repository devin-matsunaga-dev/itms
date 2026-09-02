using System.Net.Http.Json;
using System.Text.Json;
using Itms.IntegrationTests.Api;

namespace Itms.IntegrationTests.AssetsModule;

/// <summary>An asset type as the suite reads it off the wire.</summary>
/// <param name="Id">The type's id.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Description">What belongs in it.</param>
/// <param name="SortOrder">Its position in a picker.</param>
/// <param name="IsActive">False once retired.</param>
public sealed record AssetTypeDto(Guid Id, string Name, string? Description, int SortOrder, bool IsActive);

/// <summary>An asset status as the suite reads it off the wire.</summary>
/// <param name="Id">The status's id.</param>
/// <param name="Code">Its stable machine identifier.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Description">What it means.</param>
/// <param name="SortOrder">Its position in a picker.</param>
/// <param name="IsActive">False once retired.</param>
public sealed record AssetStatusDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive);

/// <summary>An asset as the suite reads it off the wire.</summary>
/// <param name="Id">The asset's id.</param>
/// <param name="AssetTag">Its unique, immutable tag.</param>
/// <param name="Name">A human label.</param>
/// <param name="SerialNumber">The manufacturer's serial.</param>
/// <param name="Manufacturer">Who made it.</param>
/// <param name="Model">What they call it.</param>
/// <param name="AssetTypeId">Its type.</param>
/// <param name="AssetTypeName">That type's current name.</param>
/// <param name="AssetStatusId">Its status.</param>
/// <param name="AssetStatusCode">That status's immutable code.</param>
/// <param name="AssetStatusName">That status's current name.</param>
/// <param name="AssignedToUserId">Who currently holds it.</param>
/// <param name="AssignedToUserName">Their display name, cached on the asset row.</param>
/// <param name="DepartmentId">The department that owns it.</param>
/// <param name="DepartmentName">That department's cached name.</param>
/// <param name="LocationId">Where it is.</param>
/// <param name="LocationPath">That location's cached full path.</param>
/// <param name="Barcode">A second scannable identifier.</param>
/// <param name="PurchaseDate">When it was bought.</param>
/// <param name="WarrantyExpiresAt">When the warranty runs out.</param>
/// <param name="Vendor">Who it was bought from.</param>
/// <param name="Cost">What it cost.</param>
/// <param name="Notes">Anything else worth recording.</param>
/// <param name="AllowedNextStatusCodes">
/// The lifecycle destinations legal from where the asset stands (WP-2.6b).
/// </param>
/// <param name="CanBeAssigned">Whether the asset may be issued, transferred, or taken back (WP-2.6b).</param>
public sealed record AssetDto(
    Guid Id,
    string AssetTag,
    string? Name,
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    Guid AssetTypeId,
    string AssetTypeName,
    Guid AssetStatusId,
    string AssetStatusCode,
    string AssetStatusName,
    Guid? AssignedToUserId,
    string? AssignedToUserName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? LocationId,
    string? LocationPath,
    string? Barcode,
    DateOnly? PurchaseDate,
    DateOnly? WarrantyExpiresAt,
    string? Vendor,
    decimal? Cost,
    string? Notes,
    IReadOnlyList<string> AllowedNextStatusCodes,
    bool CanBeAssigned);

/// <summary>One row of the asset list as the suite reads it off the wire.</summary>
/// <remarks>
/// Narrower than <see cref="AssetDto"/> on purpose, because the list row is: cost, notes,
/// barcode, vendor and the purchase date are on the detail read only. The warranty date is
/// here because the list filters and sorts on it.
/// </remarks>
/// <param name="Id">The asset's id.</param>
/// <param name="AssetTag">Its unique, immutable tag.</param>
/// <param name="Name">A human label.</param>
/// <param name="SerialNumber">The manufacturer's serial.</param>
/// <param name="Manufacturer">Who made it.</param>
/// <param name="Model">What they call it.</param>
/// <param name="AssetTypeId">Its type.</param>
/// <param name="AssetTypeName">That type's current name.</param>
/// <param name="AssetStatusId">Its status.</param>
/// <param name="AssetStatusCode">That status's immutable code.</param>
/// <param name="AssetStatusName">That status's current name.</param>
/// <param name="AssignedToUserId">Who currently holds it.</param>
/// <param name="AssignedToUserName">Their cached display name.</param>
/// <param name="DepartmentId">The department that owns it.</param>
/// <param name="DepartmentName">That department's cached name.</param>
/// <param name="LocationId">Where it is.</param>
/// <param name="LocationPath">That location's cached full path.</param>
/// <param name="WarrantyExpiresAt">When the warranty runs out.</param>
/// <param name="CreatedAt">When it was recorded.</param>
/// <param name="UpdatedAt">When it last changed.</param>
public sealed record AssetListDto(
    Guid Id,
    string AssetTag,
    string? Name,
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    Guid AssetTypeId,
    string AssetTypeName,
    Guid AssetStatusId,
    string AssetStatusCode,
    string AssetStatusName,
    Guid? AssignedToUserId,
    string? AssignedToUserName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? LocationId,
    string? LocationPath,
    DateOnly? WarrantyExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>One line of an asset's timeline as the suite reads it off the wire.</summary>
/// <param name="Id">The entry's id.</param>
/// <param name="Kind">Which dimension moved — <c>Assignment</c> or <c>Status</c>.</param>
/// <param name="FromValue">What it read before.</param>
/// <param name="ToValue">What it reads now.</param>
/// <param name="Note">What the operator said about it.</param>
/// <param name="OccurredAt">When it happened (UTC).</param>
/// <param name="Sequence">Where it sat among the entries the same operation wrote.</param>
/// <param name="ActorId">Who did it.</param>
/// <param name="ActorName">Their display name at the time.</param>
public sealed record AssetHistoryDto(
    Guid Id,
    string Kind,
    string? FromValue,
    string? ToValue,
    string? Note,
    DateTimeOffset OccurredAt,
    int Sequence,
    Guid? ActorId,
    string? ActorName);

/// <summary>The asset request shapes the suite needs, written once.</summary>
/// <remarks>
/// Uses <see cref="ApiClient"/> rather than carrying its own plumbing — STATUS.md records
/// <c>DirectoryClient</c>'s duplicate copy as something a package touching it should
/// collapse, and a new suite has no excuse to add a third.
/// </remarks>
public static class AssetsClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The route the asset endpoints hang off.</summary>
    public const string Assets = "/api/v1/assets";

    /// <summary>The route the asset-type endpoints hang off.</summary>
    public const string Types = "/api/v1/asset-types";

    /// <summary>The route the asset-status endpoints hang off.</summary>
    public const string Statuses = "/api/v1/asset-statuses";

    /// <summary>Posts an asset and returns the raw response, so a test can assert the status.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="body">The request body.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The raw response.</returns>
    public static Task<HttpResponseMessage> PostAssetAsync(
        HttpClient client,
        object body,
        CancellationToken cancellationToken) =>
        ApiClient.SendAsync(client, HttpMethod.Post, Assets, body, cancellationToken);

    /// <summary>Creates an asset and returns it, failing loudly if the call did not succeed.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="assetTag">The asset tag.</param>
    /// <param name="assetTypeId">The type to classify it as.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The created asset.</returns>
    public static async Task<AssetDto> CreateAssetAsync(
        HttpClient client,
        string assetTag,
        Guid assetTypeId,
        CancellationToken cancellationToken)
    {
        var response = await PostAssetAsync(
            client,
            new { assetTag, assetTypeId },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ApiClient.ReadAsync<AssetDto>(response, cancellationToken);
    }

    /// <summary>
    /// Corrects an asset, optionally stating an <c>If-Match</c> precondition.
    /// </summary>
    /// <remarks>
    /// Built by hand rather than through <c>ApiClient.SendAsync</c> for the reason
    /// <see cref="LifecycleAsync"/> is: that helper has nowhere to put a header, and a test
    /// has to be able to attach a deliberately stale or malformed tag.
    /// </remarks>
    /// <param name="client">A technician or admin client.</param>
    /// <param name="assetId">The asset to correct.</param>
    /// <param name="body">The request body.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="ifMatch">The entity tag to require, or null to state no precondition.</param>
    /// <returns>The raw response.</returns>
    public static async Task<HttpResponseMessage> PutAssetAsync(
        HttpClient client,
        Guid assetId,
        object body,
        CancellationToken cancellationToken,
        string? ifMatch = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(body);

        var request = new HttpRequestMessage(HttpMethod.Put, $"{Assets}/{assetId}")
        {
            Content = JsonContent.Create(body, body.GetType(), options: Json),
        };

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

    /// <summary>Creates an asset type and returns it, failing loudly if the call did not succeed.</summary>
    /// <param name="client">The admin client.</param>
    /// <param name="name">The type name.</param>
    /// <param name="sortOrder">Its position in a picker.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The created type.</returns>
    public static async Task<AssetTypeDto> CreateTypeAsync(
        HttpClient client,
        string name,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        var response = await ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            Types,
            new { name, description = (string?)null, sortOrder },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ApiClient.ReadAsync<AssetTypeDto>(response, cancellationToken);
    }

    /// <summary>Creates an asset status and returns it, failing loudly if the call did not succeed.</summary>
    /// <param name="client">The admin client.</param>
    /// <param name="code">The stable machine identifier.</param>
    /// <param name="name">The display name.</param>
    /// <param name="sortOrder">Its position in a picker.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The created status.</returns>
    public static async Task<AssetStatusDto> CreateStatusAsync(
        HttpClient client,
        string code,
        string name,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        var response = await ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            Statuses,
            new { code, name, description = (string?)null, sortOrder },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ApiClient.ReadAsync<AssetStatusDto>(response, cancellationToken);
    }

    /// <summary>The id of a seeded asset type, so a test can create an asset without inventing one.</summary>
    /// <param name="client">Any signed-in client.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The first seeded type's id.</returns>
    public static async Task<Guid> AnyTypeIdAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var page = await ApiClient.ListAsync<AssetTypeDto>(client, Types, cancellationToken);
        return page.Items[0].Id;
    }

    /// <summary>The seeded status carrying <paramref name="code"/>.</summary>
    /// <param name="client">Any signed-in client.</param>
    /// <param name="code">The status code to find.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The matching status.</returns>
    public static async Task<AssetStatusDto> StatusByCodeAsync(
        HttpClient client,
        string code,
        CancellationToken cancellationToken)
    {
        var page = await ApiClient.ListAsync<AssetStatusDto>(client, Statuses, cancellationToken);
        return page.Items.Single(status => string.Equals(status.Code, code, StringComparison.Ordinal));
    }

    /// <summary>Reads an asset and the entity tag it came back with.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="assetId">The asset to read.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The asset and its <c>ETag</c>.</returns>
    public static async Task<(AssetDto Asset, string ETag)> GetAssetAsync(
        HttpClient client,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var response = await client.GetAsync(new Uri($"{Assets}/{assetId}", UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();

        var asset = await ApiClient.ReadAsync<AssetDto>(response, cancellationToken);
        return (asset, response.Headers.ETag?.ToString() ?? string.Empty);
    }

    /// <summary>Reads a page of the asset list.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="query">The query string, without its leading <c>?</c>. Empty for no filters.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The page envelope.</returns>
    public static Task<PageDto<AssetListDto>> ListAssetsAsync(
        HttpClient client,
        string query,
        CancellationToken cancellationToken) =>
        ApiClient.ListAsync<AssetListDto>(
            client,
            string.IsNullOrEmpty(query) ? Assets : $"{Assets}?{query}",
            cancellationToken);

    /// <summary>The asset tags a list query returns, in the order it returned them.</summary>
    /// <remarks>
    /// Most of the list assertions are about <em>which</em> assets came back and in what
    /// order, and comparing tag sequences says that in one line where comparing whole rows
    /// says it in ten.
    /// </remarks>
    /// <param name="client">The signed-in client.</param>
    /// <param name="query">The query string, without its leading <c>?</c>.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The tags on the page, in order.</returns>
    public static async Task<IReadOnlyList<string>> TagsAsync(
        HttpClient client,
        string query,
        CancellationToken cancellationToken)
    {
        var page = await ListAssetsAsync(client, query, cancellationToken);
        return [.. page.Items.Select(asset => asset.AssetTag)];
    }

    /// <summary>Records an asset with the full field set, so a list test can shape one.</summary>
    /// <param name="client">A technician or admin client.</param>
    /// <param name="body">The create request body.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The created asset.</returns>
    public static async Task<AssetDto> CreateDetailedAsync(
        HttpClient client,
        object body,
        CancellationToken cancellationToken)
    {
        var response = await PostAssetAsync(client, body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ApiClient.ReadAsync<AssetDto>(response, cancellationToken);
    }

    /// <summary>Reads an asset's timeline, newest first.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="assetId">The asset whose history is wanted.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The page envelope.</returns>
    public static Task<PageDto<AssetHistoryDto>> HistoryAsync(
        HttpClient client,
        Guid assetId,
        CancellationToken cancellationToken) =>
        ApiClient.ListAsync<AssetHistoryDto>(client, $"{Assets}/{assetId}/history", cancellationToken);

    /// <summary>Issues, transfers, or takes back an asset.</summary>
    /// <param name="client">A technician or admin client.</param>
    /// <param name="assetId">The asset.</param>
    /// <param name="assignedToUserId">Who takes it on, or null to take it back.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="note">The operator's note, if any.</param>
    /// <param name="ifMatch">The entity tag to require, or null to state no precondition.</param>
    /// <returns>The raw response.</returns>
    public static Task<HttpResponseMessage> AssignAsync(
        HttpClient client,
        Guid assetId,
        Guid? assignedToUserId,
        CancellationToken cancellationToken,
        string? note = null,
        string? ifMatch = null) =>
        LifecycleAsync(client, assetId, "assignments", new { assignedToUserId, note }, cancellationToken, ifMatch);

    /// <summary>Sends an asset away to be fixed.</summary>
    /// <param name="client">A technician or admin client.</param>
    /// <param name="assetId">The asset.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="note">The operator's note, if any.</param>
    /// <param name="ifMatch">The entity tag to require, or null to state no precondition.</param>
    /// <returns>The raw response.</returns>
    public static Task<HttpResponseMessage> SendForRepairAsync(
        HttpClient client,
        Guid assetId,
        CancellationToken cancellationToken,
        string? note = null,
        string? ifMatch = null) =>
        LifecycleAsync(client, assetId, "repairs", new { note }, cancellationToken, ifMatch);

    /// <summary>Brings an asset back from repair.</summary>
    /// <param name="client">A technician or admin client.</param>
    /// <param name="assetId">The asset.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="note">The operator's note, if any.</param>
    /// <param name="ifMatch">The entity tag to require, or null to state no precondition.</param>
    /// <returns>The raw response.</returns>
    public static Task<HttpResponseMessage> ReturnToServiceAsync(
        HttpClient client,
        Guid assetId,
        CancellationToken cancellationToken,
        string? note = null,
        string? ifMatch = null) =>
        LifecycleAsync(client, assetId, "returns-to-service", new { note }, cancellationToken, ifMatch);

    /// <summary>Takes an asset out of service.</summary>
    /// <param name="client">A technician or admin client.</param>
    /// <param name="assetId">The asset.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="note">The operator's note, if any.</param>
    /// <param name="ifMatch">The entity tag to require, or null to state no precondition.</param>
    /// <returns>The raw response.</returns>
    public static Task<HttpResponseMessage> RetireAsync(
        HttpClient client,
        Guid assetId,
        CancellationToken cancellationToken,
        string? note = null,
        string? ifMatch = null) =>
        LifecycleAsync(client, assetId, "retirements", new { note }, cancellationToken, ifMatch);

    /// <summary>
    /// Posts one lifecycle call, optionally stating an <c>If-Match</c> precondition.
    /// </summary>
    /// <remarks>
    /// Built by hand rather than through <c>ApiClient.SendAsync</c>, which has nowhere to
    /// put a header: a test has to be able to attach a deliberately malformed
    /// <c>If-Match</c> without <c>HttpClient</c> validating it away. One helper for all four
    /// routes rather than <c>TicketClient</c>'s copy per route — they differ only in the
    /// segment and the body.
    /// </remarks>
    /// <param name="client">A technician or admin client.</param>
    /// <param name="assetId">The asset.</param>
    /// <param name="segment">The route segment under the asset.</param>
    /// <param name="body">The request body.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="ifMatch">The entity tag to require, or null to state no precondition.</param>
    /// <returns>The raw response.</returns>
    public static async Task<HttpResponseMessage> LifecycleAsync(
        HttpClient client,
        Guid assetId,
        string segment,
        object body,
        CancellationToken cancellationToken,
        string? ifMatch = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{Assets}/{assetId}/{segment}")
        {
            Content = JsonContent.Create(body, body.GetType(), options: Json),
        };

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
