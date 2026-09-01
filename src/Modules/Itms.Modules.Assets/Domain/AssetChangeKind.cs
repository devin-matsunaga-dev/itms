using System.Text.Json.Serialization;

namespace Itms.Modules.Assets.Domain;

/// <summary>
/// Which dimension of an asset a history entry records having moved.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two kinds, not five.</b> SPEC.md §3 names five events — assignment, transfer,
/// repair, return to service, retirement — and it is tempting to make each one a kind.
/// They are not five dimensions; they are five <em>operations</em> over two: who holds the
/// asset, and where it is in its life. A transfer is an <see cref="Assignment"/> whose
/// from-value happens to be somebody rather than nobody, and a retirement is a
/// <see cref="Status"/> move that also releases the holder. Recording the operation
/// instead of the dimension would mean a timeline that cannot answer "who had this
/// before?" without knowing which of five verbs to look under.
/// </para>
/// <para>
/// It also keeps the diff honest. One operation writes one entry per dimension it actually
/// moved, worked out by <see cref="AssetChanges.Between"/> rather than named by the handler
/// — which is why a transfer between two people writes exactly one entry (WP-2.2's
/// done-criterion) while a first assignment out of stock writes two.
/// </para>
/// <para>
/// Stored and serialised as text, following <c>TicketChangeKind</c>: a history row is read
/// at a psql prompt during an incident far more often than an enum is renumbered.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<AssetChangeKind>))]
public enum AssetChangeKind
{
    /// <summary>The asset changed hands — issued, transferred, or returned.</summary>
    Assignment,

    /// <summary>The asset moved through its lifecycle — deployed, sent for repair, retired.</summary>
    Status,
}
