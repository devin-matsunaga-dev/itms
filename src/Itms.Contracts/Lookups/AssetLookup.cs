namespace Itms.Contracts.Lookups;

/// <summary>
/// The fields another module is allowed to know about an asset. Deliberately flat and
/// small: it is a display and reference projection, not the asset aggregate, and
/// nothing outside Assets should be able to reason about lifecycle rules from it.
/// </summary>
/// <param name="Id">The asset's id.</param>
/// <param name="AssetTag">The unique, immutable asset tag (invariant 4).</param>
/// <param name="Name">The display name, suitable for a ticket or alert header.</param>
/// <param name="AssetType">The asset type, as configured in Assets.</param>
/// <param name="Status">The lifecycle status, as a string — the enum stays inside Assets.</param>
/// <param name="AssignedToUserId">The current holder, if any.</param>
/// <param name="LocationId">Where the asset is, if known.</param>
public sealed record AssetSummary(
    Guid Id,
    string AssetTag,
    string Name,
    string AssetType,
    string Status,
    Guid? AssignedToUserId,
    Guid? LocationId);

/// <summary>
/// How every other module reads assets. ARCHITECTURE.md §3 rule 2: Helpdesk,
/// Monitoring, and Alerts all need the asset backbone, and none of them may reference
/// <c>Modules.Assets</c> or query its tables — they take this instead.
/// </summary>
public interface IAssetLookup
{
    /// <summary>The asset with <paramref name="assetId"/>, or <see langword="null"/> if it does not exist or is soft-deleted.</summary>
    Task<AssetSummary?> GetAsync(Guid assetId, CancellationToken cancellationToken);

    /// <summary>
    /// The assets in <paramref name="assetIds"/> that exist. Batched so a list screen
    /// resolving twenty asset tags issues one query, not twenty.
    /// </summary>
    Task<IReadOnlyList<AssetSummary>> GetManyAsync(IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken);

    /// <summary>The asset carrying <paramref name="assetTag"/>, or <see langword="null"/>. Used by import and by device onboarding.</summary>
    Task<AssetSummary?> FindByTagAsync(string assetTag, CancellationToken cancellationToken);

    /// <summary>Everything currently assigned to <paramref name="userId"/>, for the technician's "this user's kit" view.</summary>
    Task<IReadOnlyList<AssetSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);
}
