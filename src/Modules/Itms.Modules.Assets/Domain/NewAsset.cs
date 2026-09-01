namespace Itms.Modules.Assets.Domain;

/// <summary>
/// Everything an asset must have the moment it exists, plus the optional facts a person
/// recording one usually has to hand.
/// </summary>
/// <remarks>
/// <para>
/// The tag, the type, and the status are the required three: an asset with no tag cannot
/// be found on a shelf, one with no type cannot be reported on, and one with no status is
/// not anywhere in its life. Everything else is genuinely optional — an asset can be
/// booked in from a delivery with nothing but a tag, a type, and a box.
/// </para>
/// <para>
/// <b>There is no assigned user here, deliberately.</b> ARCHITECTURE.md §11 invariant 5
/// requires an asset-history entry for an assignment, and history is WP-2.2. An asset is
/// therefore created holding nobody, and <c>Asset.AssignTo</c> — which WP-2.2 adds beside
/// the history write, in one transaction — is the only way the column is ever filled. This
/// is the same line WP-1.2 drew when it gave a ticket an <c>assignee_id</c> with no setter
/// and left it to WP-1.6.
/// </para>
/// <para>
/// The department's and the location's display strings travel with their ids because
/// ARCHITECTURE.md §3 rule 6 forbids a foreign key across a module boundary and requires an
/// id plus a cached display string instead. The caller reads them from
/// <c>IDepartmentLookup</c> and <c>ILocationLookup</c>; the type and the status are this
/// module's own rows, so they are ids alone and a rename reaches every asset for free.
/// </para>
/// </remarks>
/// <param name="AssetTag">The identifier on the physical label. Unique and immutable (invariant 4).</param>
/// <param name="AssetTypeId">What kind of thing it is, a row in this module's own table.</param>
/// <param name="AssetStatusId">Where it is in its life, a row in this module's own table.</param>
/// <param name="Name">A human label — "Reception desktop" — or <see langword="null"/> to fall back to make and model.</param>
/// <param name="SerialNumber">The manufacturer's serial, where it has one.</param>
/// <param name="Barcode">A second scannable identifier, where the organisation uses one.</param>
/// <param name="Manufacturer">Who made it.</param>
/// <param name="Model">What they call it.</param>
/// <param name="DepartmentId">The department that owns it, if any.</param>
/// <param name="DepartmentName">That department's name, cached per §3 rule 6.</param>
/// <param name="LocationId">Where it is, if known.</param>
/// <param name="LocationPath">That location's full path, cached per §3 rule 6.</param>
/// <param name="PurchaseDate">When it was bought.</param>
/// <param name="WarrantyExpiresAt">When the warranty runs out.</param>
/// <param name="Vendor">Who it was bought from.</param>
/// <param name="Cost">What it cost — see <see cref="Asset.Cost"/> on the currency.</param>
/// <param name="Notes">Anything else worth recording.</param>
public sealed record NewAsset(
    string AssetTag,
    Guid AssetTypeId,
    Guid AssetStatusId,
    string? Name,
    string? SerialNumber,
    string? Barcode,
    string? Manufacturer,
    string? Model,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? LocationId,
    string? LocationPath,
    DateOnly? PurchaseDate,
    DateOnly? WarrantyExpiresAt,
    string? Vendor,
    decimal? Cost,
    string? Notes);
