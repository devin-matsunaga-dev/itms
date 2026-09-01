using Itms.Platform.Paging;
using Microsoft.AspNetCore.Mvc;

namespace Itms.Modules.Assets.Features.Assets.ListAssets;

/// <summary>
/// Everything the asset list can be narrowed and ordered by, bound from the query string.
/// </summary>
/// <remarks>
/// <para>
/// One type rather than a dozen parameters on the endpoint delegate, so the filter set is a
/// thing with a name that OpenAPI describes and the generated client can hold. Every member
/// is optional: no filters at all is the whole estate, by asset tag.
/// </para>
/// <para>
/// <b>CONVENTIONS.md requires every list screen to keep its filters in the URL.</b> That is
/// WP-2.6's obligation, and this is the shape that makes it possible — every filter is a
/// scalar or a repeated scalar, so a view is a link somebody can paste to a colleague.
/// Nothing here is a body, and nothing here is stateful.
/// </para>
/// <para>
/// <b>There is no caller-scoped narrowing, unlike the ticket queue.</b> Every asset route is
/// Technician-or-Admin (SPEC.md §14) and an asset has no requester, so there is no
/// <c>AssetScope</c> to apply before these filters. The "equipment I hold" reading an end
/// user might want is WP-2.5's user page, which answers a different question through a
/// different route.
/// </para>
/// </remarks>
public sealed class ListAssetsQuery
{
    /// <summary>Only assets of this type.</summary>
    [FromQuery(Name = "assetTypeId")]
    public Guid? AssetTypeId { get; init; }

    /// <summary>
    /// The statuses to include. Repeat the parameter for several
    /// (<c>?assetStatusId=…&amp;assetStatusId=…</c>); omit it for every status.
    /// </summary>
    /// <remarks>
    /// Repeatable for the reason the ticket queue's status filter is: "in service" is not a
    /// status but two of them, and SPEC.md §1's asset summary tile groups several under one
    /// figure. The single-valued filters below are single because nothing has yet asked to
    /// see two departments at once.
    /// </remarks>
    [FromQuery(Name = "assetStatusId")]
    public Guid[]? AssetStatusId { get; init; }

    /// <summary>
    /// The statuses to include, named by their immutable code — <c>in-stock</c>,
    /// <c>deployed</c>, <c>repair</c>, <c>retired</c>, <c>lost</c>, <c>disposed</c>.
    /// Repeatable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The second way to name a status, and it earns its place: an id belongs to one
    /// database, while a code is the same in every deployment. A dashboard tile, a seeded
    /// link, or a saved view written against <c>?statusCode=deployed</c> survives a rebuild
    /// and a restore; one written against an id does not. WP-2.1 gave a status a code
    /// precisely so something stable could be reasoned about.
    /// </para>
    /// <para>
    /// Given alongside <see cref="AssetStatusId"/> it narrows, as every filter here does —
    /// the result is the assets matching both sets. An unrecognised code is not an error; it
    /// is a filter matching nothing, which is what it says.
    /// </para>
    /// </remarks>
    [FromQuery(Name = "statusCode")]
    public string[]? StatusCode { get; init; }

    /// <summary>Only assets owned by this department.</summary>
    [FromQuery(Name = "departmentId")]
    public Guid? DepartmentId { get; init; }

    /// <summary>Only assets at this location.</summary>
    /// <remarks>
    /// Matches the location exactly and does not descend the tree. WP-0.6 built a
    /// materialised path that makes "everything in this building" answerable, but doing it
    /// here means Assets reasoning about Directory's path format, which §3 rule 6 keeps it
    /// out of. WP-2.4 owns the cascading picker and is where a subtree filter belongs.
    /// </remarks>
    [FromQuery(Name = "locationId")]
    public Guid? LocationId { get; init; }

    /// <summary>Only assets this person holds.</summary>
    /// <remarks>Ignored when <see cref="Unassigned"/> is true — nobody holds an unassigned asset.</remarks>
    [FromQuery(Name = "assignedToUserId")]
    public Guid? AssignedToUserId { get; init; }

    /// <summary>Only assets nobody holds.</summary>
    /// <remarks>
    /// A flag of its own rather than an absent <see cref="AssignedToUserId"/>, because "no
    /// filter on the holder" and "filter to those with no holder" are different questions
    /// and a null cannot mean both. The call WP-1.5 made for an unassigned ticket.
    /// </remarks>
    [FromQuery(Name = "unassigned")]
    public bool? Unassigned { get; init; }

    /// <summary>
    /// Only assets whose warranty runs out within this many days — the filter WP-2.3's
    /// done-criterion names, and the query behind SPEC.md §1's expiry tile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Inclusive at both ends, and already-lapsed warranties are excluded.</b>
    /// <c>warrantyExpiringInDays=30</c> is <c>today &lt;= expiry &lt;= today + 30</c>. Zero
    /// is the warranties running out today. An asset with no warranty date recorded is not
    /// matched — there is no date to be expiring.
    /// </para>
    /// <para>
    /// "Today" is the server's UTC date from <c>IClock</c>, not a window the caller
    /// manufactures: a warranty expiry is a <see cref="DateOnly"/>, a calendar fact with no
    /// zone, so unlike WP-1.12's <c>dueBefore</c> there is nothing here for a client's own
    /// clock to decide. The tile is therefore
    /// <c>?warrantyExpiringInDays=30&amp;sort=WarrantyExpiresAt&amp;direction=Ascending</c>
    /// — soonest first, which is the order SPEC.md §1 asks for.
    /// </para>
    /// <para>
    /// A negative value matches nothing rather than failing, and an absurd one saturates at
    /// the end of the calendar. See <see cref="WarrantyWindow.UpperBound"/>.
    /// </para>
    /// </remarks>
    [FromQuery(Name = "warrantyExpiringInDays")]
    public int? WarrantyExpiringInDays { get; init; }

    /// <summary>
    /// <see langword="true"/> for assets whose warranty has already run out,
    /// <see langword="false"/> for those it has not, omitted for both.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A separate filter rather than a wider <see cref="WarrantyExpiringInDays"/> window,
    /// because "lapsing soon" and "already lapsed" are different operational facts: one is
    /// something to renew and the other is something to explain. An asset with no warranty
    /// date has not expired, so <c>warrantyExpired=false</c> includes it.
    /// </para>
    /// <para>
    /// <b>Given as <see langword="true"/> alongside <see cref="WarrantyExpiringInDays"/>,
    /// the two combine as a union rather than narrowing</b> — "already lapsed, or lapsing
    /// within N days", which is the one list somebody chasing warranties actually wants. It
    /// is the only reading that is not empty: the two windows are disjoint by construction,
    /// so intersecting them could never return a row, and no caller asks a question whose
    /// only possible answer is nothing. Every other pairing on this type narrows.
    /// </para>
    /// </remarks>
    [FromQuery(Name = "warrantyExpired")]
    public bool? WarrantyExpired { get; init; }

    /// <summary>
    /// Free text matched against the asset tag, serial number, name, manufacturer, and
    /// model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately narrow, exactly as the ticket queue's is: a case-insensitive "contains"
    /// over five columns of one table, not the cross-entity search <c>WP-4.2</c> builds.
    /// Nothing here enables an extension or adds an index — a list search is a filter, and
    /// the global search is a different feature with a different shape.
    /// </para>
    /// <para>
    /// <b>Hostname is not searched, because assets do not have one yet.</b> WP-2.3's text
    /// names it alongside tag and serial, but SPEC.md §3's network fields — hostname, IP,
    /// the monitoring flag, the SNMP settings — arrive at <c>WP-3.1</c>, which projects a
    /// monitored device over an asset. Adding a placeholder column here to satisfy the
    /// wording would be inventing the shape that package has to design. <b>WP-3.1 inherits
    /// the obligation:</b> the package that gives an asset a hostname adds it to this
    /// search.
    /// </para>
    /// <para>
    /// The notes column is left out for WP-1.12's reason: it is four thousand characters and
    /// searching it would make every keystroke a scan of every asset's free text.
    /// </para>
    /// </remarks>
    [FromQuery(Name = "search")]
    public string? Search { get; init; }

    /// <summary>What to order by. Defaults to <see cref="AssetSort.AssetTag"/>.</summary>
    [FromQuery(Name = "sort")]
    public AssetSort? Sort { get; init; }

    /// <summary>
    /// Which way to order. Defaults to <see cref="SortDirection.Ascending"/> for
    /// <see cref="AssetSort.AssetTag"/> and <see cref="AssetSort.WarrantyExpiresAt"/>, where
    /// the useful end is the front, and to <see cref="SortDirection.Descending"/> otherwise.
    /// </summary>
    [FromQuery(Name = "direction")]
    public SortDirection? Direction { get; init; }

    /// <summary>The 1-based page number. Out-of-range values are clamped, not rejected.</summary>
    [FromQuery(Name = "page")]
    public int? Page { get; init; }

    /// <summary>How many assets to a page, up to the API-wide maximum of 200.</summary>
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; }
}
