namespace Itms.Modules.Assets.Domain;

/// <summary>
/// The two dimensions of an asset that <see cref="AssetHistoryEntry"/> records, read off
/// the entity at one instant.
/// </summary>
/// <remarks>
/// <para>
/// A handler takes one of these <em>before</em> it calls a lifecycle method and hands it
/// to <c>AssetHistoryRecorder</c> afterwards. That is the whole point of the type:
/// invariant 5 requires a history entry for assignment, transfer, repair, return to
/// service and retirement, and a handler that has to remember <em>which</em> entries its
/// own operation produces is a handler that will eventually forget one. Capturing the
/// before-state is the only thing a caller has to remember, and forgetting it is not
/// silent — no entries are written at all, which the tests catch.
/// </para>
/// <para>
/// It carries the status's <see cref="StatusName"/> as well as its id because the status
/// lives on another row and the entry records the name it read at the time. The asset
/// itself holds only the id, so the snapshot is taken from the asset <em>and</em> the
/// resolved status row together.
/// </para>
/// <para>
/// It deliberately holds no department, location, cost, or notes. Those change too, and an
/// edit path will audit them; they are not what the lifecycle timeline is for.
/// </para>
/// </remarks>
/// <param name="AssignedToUserId">Who held it, or <see langword="null"/> when nobody did.</param>
/// <param name="AssignedToUserName">Their display name as the asset cached it, or <see langword="null"/>.</param>
/// <param name="StatusId">Which status row it carried.</param>
/// <param name="StatusCode">That status's immutable code — what the events carry.</param>
/// <param name="StatusName">That status's display name at the time — what the history entry records.</param>
public readonly record struct AssetSnapshot(
    Guid? AssignedToUserId,
    string? AssignedToUserName,
    Guid StatusId,
    string StatusCode,
    string StatusName)
{
    /// <summary>Reads the two tracked dimensions off an asset as it stands right now.</summary>
    /// <param name="asset">The asset to read.</param>
    /// <param name="status">
    /// The status row the asset currently carries. Must be the asset's own — the caller
    /// resolved it, and passing another asset's status would silently describe the wrong
    /// move.
    /// </param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="ArgumentException"><paramref name="status"/> is not the asset's current status.</exception>
    public static AssetSnapshot Of(Asset asset, AssetStatusRef status)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (status.Id != asset.AssetStatusId)
        {
            throw new ArgumentException(
                "The status supplied is not the one the asset currently carries.",
                nameof(status));
        }

        return new AssetSnapshot(
            asset.AssignedToUserId,
            asset.AssignedToUserName,
            status.Id,
            status.Code,
            status.Name);
    }
}
