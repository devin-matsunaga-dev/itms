namespace Itms.Modules.Assets.Features.Assets;

/// <summary>One row of the asset list, as the API renders it.</summary>
/// <remarks>
/// <para>
/// Every field a list screen draws is here, so the list is one round trip and never a row
/// followed by a lookup per row. The type's and the status's names are joined in from this
/// module's own tables — which is why a rename reaches every row for free — while the
/// holder, department and location strings are read from the asset's own cached columns
/// (§3 rule 6), because those belong to other modules and a foreign key across the boundary
/// is forbidden.
/// </para>
/// <para>
/// <b>It is narrower than <see cref="AssetResponse"/>, deliberately.</b> Cost, notes,
/// barcode, vendor and the purchase date are on the detail read and not here: none of them
/// is something a list is scanned by, notes is a four-thousand-character column, and cost is
/// the field a screenshot of an inventory list should not casually carry. A row that names
/// them would make every page of two hundred assets pay for them.
/// </para>
/// <para>
/// <b>The status carries its code as well as its name</b>, for the reason
/// <c>TicketListItemResponse</c> carries the priority's: DESIGN.md fixes a colour per
/// lifecycle state and WP-2.1 gave a status an immutable code precisely so a rename cannot
/// move that key. A client colours on <see cref="AssetStatusCode"/> and labels on
/// <see cref="AssetStatusName"/>.
/// </para>
/// </remarks>
/// <param name="Id">The asset's id.</param>
/// <param name="AssetTag">The identifier on the physical label. Unique and immutable.</param>
/// <param name="Name">A human label, or <see langword="null"/> to fall back to make and model.</param>
/// <param name="SerialNumber">The manufacturer's serial, where it has one.</param>
/// <param name="Manufacturer">Who made it.</param>
/// <param name="Model">What they call it.</param>
/// <param name="AssetTypeId">What kind of thing it is.</param>
/// <param name="AssetTypeName">That type's name, as it reads now.</param>
/// <param name="AssetStatusId">Where it is in its life.</param>
/// <param name="AssetStatusCode">That status's stable key, for colour and for rules.</param>
/// <param name="AssetStatusName">That status's name, as it reads now.</param>
/// <param name="AssignedToUserId">Who currently holds it, or <see langword="null"/>.</param>
/// <param name="AssignedToUserName">Their display name, cached at assignment.</param>
/// <param name="DepartmentId">The department that owns it, or <see langword="null"/>.</param>
/// <param name="DepartmentName">That department's cached name.</param>
/// <param name="LocationId">Where it is, or <see langword="null"/>.</param>
/// <param name="LocationPath">That location's cached full path.</param>
/// <param name="WarrantyExpiresAt">
/// When the warranty runs out, or <see langword="null"/> when none was recorded. The list
/// sorts and filters on this, so a row can render its own expiry state without a second call.
/// </param>
/// <param name="CreatedAt">When the asset was recorded (UTC).</param>
/// <param name="UpdatedAt">When it last changed (UTC).</param>
public sealed record AssetListItemResponse(
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
    DateTimeOffset UpdatedAt)
{
    // No projection expression here, deliberately — the same note TicketListItemResponse
    // carries. A list has to be joined and ordered before it is projected, because EF cannot
    // translate an OrderBy written over a constructed record and the status sort orders by
    // the status's SortOrder, which is not a column on the asset. ListAssetsHandler joins,
    // orders, pages, and only then builds this shape. It is the only query that produces one.
}
