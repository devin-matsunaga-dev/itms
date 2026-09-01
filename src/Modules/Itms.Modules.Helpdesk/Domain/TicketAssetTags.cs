namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// The two asset tags a link change moved between, as they read at the time.
/// </summary>
/// <remarks>
/// <para>
/// The same problem <see cref="TicketPriorityNames"/> solves, one boundary further out: a
/// ticket holds its related asset as a bare id (§3 rule 6), and the tag that names it is
/// not merely on another row — it is in another module, read through <c>IAssetLookup</c>.
/// <c>TicketHistoryRecorder</c> resolves both, and only when the ids actually differ, then
/// hands them to <see cref="TicketChanges.Between"/> so that function stays pure.
/// </para>
/// <para>
/// Both sides are nullable because linking and unlinking are the same change in opposite
/// directions: attaching an asset to a ticket that had none moves from nothing, and
/// detaching one moves to nothing. A timeline that recorded only the first would lose the
/// correction of a mislinked ticket, which is the one a technician most wants to see.
/// </para>
/// </remarks>
/// <param name="From">The tag of the asset the ticket named before, or <see langword="null"/> when it named none.</param>
/// <param name="To">The tag it names now, or <see langword="null"/> when the link was cleared.</param>
public readonly record struct TicketAssetTags(string? From, string? To);
