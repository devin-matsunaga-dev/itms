using Itms.Modules.Assets.Domain;

namespace Itms.Modules.Assets.Features.Assets;

/// <summary>
/// An asset as the API renders it.
/// </summary>
/// <remarks>
/// The type's and the status's names are resolved by joining this module's own tables at
/// read time rather than being cached on the asset row — which is exactly why a rename
/// reaches every asset for free. The department and location strings are the opposite case:
/// they belong to Directory, §3 rule 6 forbids the foreign key, so they are cached and are
/// as fresh as the last time the asset was written. See STATUS.md on the rename events that
/// would refresh them.
/// </remarks>
/// <param name="Id">The asset's id.</param>
/// <param name="AssetTag">The identifier on the physical label. Unique and immutable.</param>
/// <param name="Name">A human label, or <see langword="null"/>.</param>
/// <param name="SerialNumber">The manufacturer's serial, where it has one.</param>
/// <param name="Barcode">A second scannable identifier, where the organisation uses one.</param>
/// <param name="Manufacturer">Who made it.</param>
/// <param name="Model">What they call it.</param>
/// <param name="AssetTypeId">What kind of thing it is.</param>
/// <param name="AssetTypeName">That type's current name.</param>
/// <param name="AssetStatusId">Where it is in its life.</param>
/// <param name="AssetStatusCode">That status's immutable code — what a client keys colours and rules off.</param>
/// <param name="AssetStatusName">That status's current name.</param>
/// <param name="AssignedToUserId">Who currently holds it.</param>
/// <param name="AssignedToUserName">Their cached display name.</param>
/// <param name="DepartmentId">The department that owns it.</param>
/// <param name="DepartmentName">That department's cached name.</param>
/// <param name="LocationId">Where it is.</param>
/// <param name="LocationPath">That location's cached full path.</param>
/// <param name="PurchaseDate">When it was bought.</param>
/// <param name="WarrantyExpiresAt">When the warranty runs out.</param>
/// <param name="Vendor">Who it was bought from.</param>
/// <param name="Cost">What it cost, in the deployment's own currency — there is only one.</param>
/// <param name="Notes">Anything else worth recording.</param>
/// <param name="CreatedAt">When the record was created (UTC).</param>
/// <param name="UpdatedAt">When it was last changed (UTC).</param>
public sealed record AssetResponse(
    Guid Id,
    string AssetTag,
    string? Name,
    string? SerialNumber,
    string? Barcode,
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
    DateOnly? PurchaseDate,
    DateOnly? WarrantyExpiresAt,
    string? Vendor,
    decimal? Cost,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Renders entities the handler already has in memory.</summary>
    /// <param name="asset">The asset to render.</param>
    /// <param name="type">Its type, already loaded.</param>
    /// <param name="status">Its status, already loaded.</param>
    /// <returns>The response shape.</returns>
    internal static AssetResponse From(Asset asset, AssetType type, AssetStatus status)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(status);

        return From(asset, type.Id, type.Name, AssetStatusRef.Of(status));
    }

    /// <summary>
    /// Renders an asset whose type and status the caller resolved separately.
    /// </summary>
    /// <remarks>
    /// A lifecycle operation works in <see cref="AssetStatusRef"/> rather than in status
    /// entities — it has to reason about codes, not rows — and reloading the entity just to
    /// render the response would be a query to fetch text it is already holding. One shape,
    /// two ways in.
    /// </remarks>
    /// <param name="asset">The asset to render.</param>
    /// <param name="assetTypeId">Its type's id.</param>
    /// <param name="assetTypeName">That type's current name.</param>
    /// <param name="status">The status it carries now.</param>
    /// <returns>The response shape.</returns>
    internal static AssetResponse From(Asset asset, Guid assetTypeId, string assetTypeName, AssetStatusRef status)
    {
        ArgumentNullException.ThrowIfNull(asset);

        return new AssetResponse(
            asset.Id,
            asset.AssetTag,
            asset.Name,
            asset.SerialNumber,
            asset.Barcode,
            asset.Manufacturer,
            asset.Model,
            assetTypeId,
            assetTypeName,
            status.Id,
            status.Code,
            status.Name,
            asset.AssignedToUserId,
            asset.AssignedToUserName,
            asset.DepartmentId,
            asset.DepartmentName,
            asset.LocationId,
            asset.LocationPath,
            asset.PurchaseDate,
            asset.WarrantyExpiresAt,
            asset.Vendor,
            asset.Cost,
            asset.Notes,
            asset.CreatedAt,
            asset.UpdatedAt);
    }
}
