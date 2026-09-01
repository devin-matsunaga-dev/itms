using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Identity;

namespace Itms.Modules.Assets.Features.AssetHistory;

/// <summary>
/// Writes an asset's timeline. Every operation that moves a holder or a lifecycle status
/// goes through here.
/// </summary>
/// <remarks>
/// <para>
/// <b>It adds, it does not save.</b> The entries are added to the caller's own
/// <see cref="AssetsDbContext"/> and go to the database on the caller's own
/// <c>SaveChangesAsync</c>, inside the caller's own transaction. That is what invariant 5
/// asks for — the asset-history entry written in the same transaction as the change — and
/// it is why a rolled-back transfer cannot leave an orphan line claiming it happened. A
/// recorder that saved for itself would be a second commit, and the whole guarantee would
/// be gone.
/// </para>
/// <para>
/// <b>Who and when come from here, what moved comes from
/// <see cref="AssetChanges.Between"/>.</b> The actor is read from
/// <see cref="ICurrentUser"/> rather than passed in, so no call site can attribute a
/// change to somebody else by accident; the instant is passed in, because it has to be the
/// same one the change itself wrote and the caller already has it.
/// </para>
/// <para>
/// Public rather than internal, following the call WP-1.4 made for
/// <c>TicketHistoryRecorder</c>: the integration suite has to prove that a rolled-back
/// transaction leaves no orphan line, and the only way to prove that against the recorder
/// callers actually use is to drive it from a transaction the test controls. No module can
/// reference Assets anyway, and the alternative is an <c>InternalsVisibleTo</c> this
/// repository has nowhere else.
/// </para>
/// </remarks>
/// <param name="database">The caller's assets context, already enlisted in its transaction.</param>
/// <param name="currentUser">Who is making the request.</param>
public sealed class AssetHistoryRecorder(AssetsDbContext database, ICurrentUser currentUser)
{
    /// <summary>
    /// Adds the history entries owed for the move from <paramref name="before"/> to the
    /// asset as it now stands.
    /// </summary>
    /// <remarks>
    /// Nothing is added when nothing tracked moved. A caller does not have to check first:
    /// an operation that only touched the notes writes no timeline line, which is correct.
    /// </remarks>
    /// <param name="asset">The asset, after the operation has been applied to it.</param>
    /// <param name="before">The snapshot taken before it.</param>
    /// <param name="status">The status the asset carries now, resolved by the caller.</param>
    /// <param name="occurredAt">When the change happened (UTC) — the same instant the change wrote.</param>
    /// <param name="note">What the operator said about it, or <see langword="null"/>.</param>
    /// <returns>The entries added, in timeline order. Empty when nothing tracked moved.</returns>
    public IReadOnlyList<AssetHistoryEntry> Record(
        Asset asset,
        AssetSnapshot before,
        AssetStatusRef status,
        DateTimeOffset occurredAt,
        string? note)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var after = AssetSnapshot.Of(asset, status);
        var changes = AssetChanges.Between(before, after);

        if (changes.Count == 0)
        {
            return [];
        }

        // The ordinal is what orders the lines a single operation writes: they share an
        // instant, so nothing else can. AssetChanges.Between fixes the order they come in.
        var entries = changes
            .Select((change, sequence) => AssetHistoryEntry.Record(
                asset.Id,
                change,
                sequence,
                occurredAt,
                note,
                currentUser.UserId,
                currentUser.DisplayName))
            .ToList();

        database.AssetHistory.AddRange(entries);

        return entries;
    }
}
