using System.Text.Json.Serialization;

namespace Itms.Platform.Paging;

/// <summary>Which way a sorted list runs.</summary>
/// <remarks>
/// <para>
/// Shared-kernel material by ARCHITECTURE.md §3 rule 4's definition — a genuinely shared
/// primitive that references no module, and the natural companion to
/// <see cref="PageRequest"/>: a list that is paged is a list that has to be ordered, or the
/// pages do not mean anything.
/// </para>
/// <para>
/// <b>It arrived here at WP-2.3, on the trigger it was written carrying.</b> WP-1.5
/// declared it inside Helpdesk as the first list in the system that sorted by anything but
/// a fixed order, and said in as many words that the second module to want it should move
/// it beside <see cref="PageRequest"/>. The asset list is that second module. Nothing about
/// the type changed in the move, and neither did the OpenAPI schema — the document names
/// schemas by unqualified type name, so the generated client is untouched.
/// </para>
/// <para>
/// <b>A module still declares its own sort enum.</b> Only the direction is shared; what a
/// list may be ordered <em>by</em> is a closed set specific to that list, because a sort
/// reaching the database as free text is either a scan of an unindexed column or an
/// injection question nobody wants to answer.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<SortDirection>))]
public enum SortDirection
{
    /// <summary>Smallest, earliest, or lowest first.</summary>
    Ascending,

    /// <summary>Largest, latest, or highest first.</summary>
    Descending,
}
