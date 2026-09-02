namespace Itms.Modules.Assets.Features.Assets.UpdateAsset;

/// <summary>
/// The fields an existing asset may be corrected to.
/// </summary>
/// <remarks>
/// <para>
/// <b>A full replacement of the descriptive half, which is why it is a <c>PUT</c>.</b>
/// Every field here is written, and a field omitted from the body is read as
/// <see langword="null"/> — cleared, not left alone. That is the honest reading of a PUT
/// and it is what the edit form sends: the form holds every one of these and posts all of
/// them, so "the user emptied the vendor box" and "the client forgot to send vendor" are
/// the same request and must mean the same thing. A partial edit would be a PATCH with a
/// way to tell absent from null, and nothing has asked for one.
/// </para>
/// <para>
/// <b>Three fields are deliberately absent</b> and each absence is an invariant rather than
/// an oversight — see <c>AssetEdit</c>, which this maps onto. There is no
/// <c>assetTag</c> (invariant 4 makes it immutable), no <c>assetStatusId</c> (a lifecycle
/// move goes through the four lifecycle routes, which write history and publish events),
/// and no <c>assignedToUserId</c> (<c>POST /assets/{id}/assignments</c> owns that column).
/// A caller that sends one of them is not refused — the field simply is not part of this
/// shape, so it never reaches the entity.
/// </para>
/// </remarks>
/// <param name="AssetTypeId">What kind of thing it is. Required.</param>
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
public sealed record UpdateAssetRequest(
    Guid AssetTypeId,
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
