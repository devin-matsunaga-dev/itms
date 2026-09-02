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
    /// <summary>
    /// The status codes this asset may legally be moved to next, straight from
    /// <see cref="AssetLifecycle.DestinationsFrom"/>. Empty from a terminal status, and
    /// empty from a custom status the lifecycle table does not know.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This exists so a client never restates the lifecycle table in its own language.</b>
    /// It is the asset's mirror of <c>TicketDetailResponse.AllowedNextStatuses</c>, and
    /// <see cref="AssetLifecycle.DestinationsFrom"/>'s own doc comment asks for it by name:
    /// WP-2.6b renders the lifecycle actions and an illegal one has to be <em>absent</em>
    /// rather than disabled, which is only true for as long as the buttons are told rather
    /// than deciding.
    /// </para>
    /// <para>
    /// Codes rather than ids, and codes rather than names. A status is a configurable row
    /// an administrator may rename, and the code is the immutable thing WP-2.1 added
    /// precisely so something stable could be reasoned about — the lifecycle table is keyed
    /// on it and so is DESIGN.md's colour map.
    /// </para>
    /// <para>
    /// Computed on every read, never stored. Set after projection, like its ticket
    /// counterpart, because it is derived from the status and not a column.
    /// </para>
    /// </remarks>
    public IReadOnlyCollection<string> AllowedNextStatusCodes { get; init; } = [];

    /// <summary>
    /// Whether this asset may be issued to somebody, transferred, or taken back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Assignment is a different fact from a lifecycle move, so it needs its own
    /// answer.</b> <see cref="AllowedNextStatusCodes"/> cannot carry it:
    /// <see cref="AssetLifecycle.DestinationsFrom"/> is empty both for a terminal status
    /// <em>and</em> for a custom status an administrator added, and those two cases differ —
    /// <see cref="Asset.AssignTo"/> refuses only the terminal three, deliberately, so that
    /// adding a status does not quietly make the equipment in it unissuable.
    /// </para>
    /// <para>
    /// <b>Strictly derived, never stored and never set by hand.</b> It is
    /// <c>!AssetLifecycle.IsTerminal(code)</c> — the same call the entity makes — evaluated
    /// on every read from the status the asset carries at that moment. There is no setter a
    /// handler could reach for and no column it could drift from; it is a convenience
    /// projection of a rule that lives in one place. The client's buttons are not the
    /// enforcement either: <see cref="Asset.AssignTo"/> refuses a terminal asset with
    /// <c>assets.asset_not_assignable</c> whatever a caller sends.
    /// </para>
    /// </remarks>
    public bool CanBeAssigned { get; init; }

    /// <summary>
    /// Fills the two derived lifecycle fields from the status this response already
    /// carries.
    /// </summary>
    /// <remarks>
    /// One call, made by both <c>From</c> overloads and by the read that projects in the
    /// query, so no construction path can produce a response whose buttons would be wrong.
    /// It takes no argument on purpose: the code it reasons about is the one on the
    /// response, which is the one the client will be looking at.
    /// </remarks>
    /// <returns>The same response with the derived fields set.</returns>
    internal AssetResponse WithLifecycle() =>
        this with
        {
            AllowedNextStatusCodes = [.. AssetLifecycle.DestinationsFrom(AssetStatusCode)],
            CanBeAssigned = !AssetLifecycle.IsTerminal(AssetStatusCode),
        };

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
            asset.UpdatedAt)
            .WithLifecycle();
    }
}
