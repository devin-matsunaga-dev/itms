namespace Itms.Modules.Assets.Features.Assets.CreateAsset;

/// <summary>
/// The fields a new asset is recorded from.
/// </summary>
/// <remarks>
/// <b>There is no assigned user.</b> Invariant 5 requires an asset-history entry for an
/// assignment and history is WP-2.2, so an asset is created holding nobody and
/// <c>POST /api/v1/assets/{id}/assignments</c> — WP-2.2's endpoint — is what fills it.
/// </remarks>
/// <param name="AssetTag">The identifier on the physical label. Required, unique, and immutable once set.</param>
/// <param name="AssetTypeId">What kind of thing it is. Required.</param>
/// <param name="AssetStatusId">
/// Where it is in its life. Optional: omitted, the asset starts in the seeded
/// <c>in-stock</c> status.
/// </param>
/// <param name="Name">A human label, or <see langword="null"/>.</param>
/// <param name="SerialNumber">The manufacturer's serial, where it has one.</param>
/// <param name="Barcode">A second scannable identifier, where the organisation uses one.</param>
/// <param name="Manufacturer">Who made it.</param>
/// <param name="Model">What they call it.</param>
/// <param name="DepartmentId">The department that owns it.</param>
/// <param name="LocationId">Where it is.</param>
/// <param name="PurchaseDate">When it was bought.</param>
/// <param name="WarrantyExpiresAt">When the warranty runs out.</param>
/// <param name="Vendor">Who it was bought from.</param>
/// <param name="Cost">What it cost, in the deployment's own currency — there is only one.</param>
/// <param name="Notes">Anything else worth recording.</param>
public sealed record CreateAssetRequest(
    string AssetTag,
    Guid AssetTypeId,
    Guid? AssetStatusId,
    string? Name,
    string? SerialNumber,
    string? Barcode,
    string? Manufacturer,
    string? Model,
    Guid? DepartmentId,
    Guid? LocationId,
    DateOnly? PurchaseDate,
    DateOnly? WarrantyExpiresAt,
    string? Vendor,
    decimal? Cost,
    string? Notes);
