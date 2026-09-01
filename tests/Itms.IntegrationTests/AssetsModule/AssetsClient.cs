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
/// <param name="AssignedToUserId">Who holds it. Null until WP-2.2.</param>
/// <param name="DepartmentId">The department that owns it.</param>
/// <param name="DepartmentName">That department's cached name.</param>
/// <param name="LocationId">Where it is.</param>
/// <param name="LocationPath">That location's cached full path.</param>
/// <param name="Cost">What it cost.</param>
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
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? LocationId,
    string? LocationPath,
    decimal? Cost);

/// <summary>The asset request shapes the suite needs, written once.</summary>
/// <remarks>
/// Uses <see cref="ApiClient"/> rather than carrying its own plumbing — STATUS.md records
/// <c>DirectoryClient</c>'s duplicate copy as something a package touching it should
/// collapse, and a new suite has no excuse to add a third.
/// </remarks>
public static class AssetsClient
{
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
}
