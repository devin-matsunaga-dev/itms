using System.Text.Json.Serialization;

namespace Itms.Modules.Assets.Features.Assets.ListAssets;

/// <summary>What the asset list is ordered by.</summary>
/// <remarks>
/// <para>
/// A closed set rather than a free-text column name, for WP-1.5's reason: a sort that
/// reaches the database as a string is either a table scan on an unindexed column or an
/// injection question nobody wants to have to answer. An unrecognised value is a 400 from
/// model binding, not a silent fallback.
/// </para>
/// <para>
/// <b>The default is <see cref="AssetTag"/> ascending</b>, where the ticket queue defaults
/// to newest first. An inventory is not a queue: it is a register, read against the labels
/// on physical equipment, and somebody looking for <c>LAP-0042</c> wants the list to run in
/// the order the labels do. "What was added recently" is a real question and it is
/// <see cref="CreatedAt"/> — asked for, rather than assumed on the reader's behalf.
/// </para>
/// <para>
/// The direction only shares its type with Helpdesk. <c>SortDirection</c> moved into
/// <c>Itms.Platform.Paging</c> at WP-2.3 on the trigger WP-1.5 wrote into it; what a list
/// may be ordered <em>by</em> stays specific to the list, which is why this enum exists at
/// all.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<AssetSort>))]
public enum AssetSort
{
    /// <summary>
    /// The tag on the physical label. The default, ascending, and ordered on the normalised
    /// form so <c>lap-0042</c> sorts where <c>LAP-0042</c> would.
    /// </summary>
    AssetTag,

    /// <summary>When the asset was recorded.</summary>
    CreatedAt,

    /// <summary>When the asset last moved.</summary>
    UpdatedAt,

    /// <summary>
    /// When the warranty runs out — soonest first by default, which is the ordering
    /// SPEC.md §1's expiry tile names. Assets with no warranty date sort last on the way
    /// up, because "no date recorded" is not "expiring imminently".
    /// </summary>
    WarrantyExpiresAt,

    /// <summary>
    /// Where the asset is in its life, ordered by the status's own <c>SortOrder</c> rather
    /// than its name — the same position an administrator gave it in every picker, so one
    /// list cannot disagree with another about what order the statuses come in.
    /// </summary>
    Status,
}
