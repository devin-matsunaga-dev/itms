namespace Itms.Modules.Assets.Domain;

/// <summary>
/// Everything about an asset that a correction may move.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is <see cref="NewAsset"/> with three fields taken out, and each absence is an
/// invariant.</b> There is no asset tag, because invariant 4 makes it immutable once
/// created — <see cref="Asset"/> exposes no way to write it, and a field here would be an
/// invitation to add one. There is no status, because a lifecycle move has to go through
/// <see cref="AssetLifecycle"/>, write a history entry (invariant 5), and publish
/// <c>AssetStatusChanged</c>; an edit that could set the column would route round all
/// three. And there is no holder, for the same reason the create has none —
/// <see cref="Asset.AssignTo"/> is the only way that column is ever written.
/// </para>
/// <para>
/// So this record is deliberately the <em>descriptive</em> half of an asset: what the
/// thing is, where it belongs, and what it cost. Correcting a mistyped serial is a
/// correction; moving equipment through its life is not, and the two must not share a
/// route.
/// </para>
/// <para>
/// The department's and the location's display strings travel with their ids for the same
/// reason they do on <see cref="NewAsset"/>: ARCHITECTURE.md §3 rule 6 forbids a foreign
/// key across a module boundary and requires an id plus a cached display string, which the
/// caller reads from <c>IDepartmentLookup</c> and <c>ILocationLookup</c>.
/// </para>
/// </remarks>
/// <param name="AssetTypeId">What kind of thing it is, a row in this module's own table.</param>
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
public sealed record AssetEdit(
    Guid AssetTypeId,
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
    string? Notes)
{
    /// <summary>
    /// The descriptive half of an asset as it currently reads — the "before" of an edit.
    /// </summary>
    /// <remarks>
    /// Paired with what <see cref="Asset.Update"/> returns, this is how the handler works
    /// out which fields actually moved, so the audit entry records changed fields only
    /// (ARCHITECTURE.md §8) rather than making every edit look like a rewrite of the row.
    /// It is also what lets the entity answer "nothing moved" in one comparison, because a
    /// record compares by value.
    /// </remarks>
    /// <param name="asset">The asset to read.</param>
    /// <returns>Its current descriptive state.</returns>
    public static AssetEdit Of(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        return new AssetEdit(
            asset.AssetTypeId,
            asset.Name,
            asset.SerialNumber,
            asset.Barcode,
            asset.Manufacturer,
            asset.Model,
            asset.DepartmentId,
            asset.DepartmentName,
            asset.LocationId,
            asset.LocationPath,
            asset.PurchaseDate,
            asset.WarrantyExpiresAt,
            asset.Vendor,
            asset.Cost,
            asset.Notes);
    }
}
