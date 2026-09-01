namespace Itms.Modules.Assets.Domain;

/// <summary>
/// One movement a lifecycle operation produced: which dimension moved, and the two
/// display values it moved between.
/// </summary>
/// <remarks>
/// The shape before it becomes a row. <c>AssetHistoryRecorder</c> is what turns it into an
/// <see cref="AssetHistoryEntry"/> with an actor, an instant, and the operator's note
/// attached; the separation is what lets <see cref="AssetChanges.Between"/> be a pure
/// function the unit suite can exhaust without a database. This is the shape WP-1.4 gave
/// a ticket's timeline, and it is deliberately the same one.
/// </remarks>
/// <param name="Kind">Which dimension moved.</param>
/// <param name="From">
/// What it read before, or <see langword="null"/> when there was nothing there — an asset
/// nobody was holding.
/// </param>
/// <param name="To">What it reads now, or <see langword="null"/> when the change cleared it.</param>
public readonly record struct AssetChange(AssetChangeKind Kind, string? From, string? To);
