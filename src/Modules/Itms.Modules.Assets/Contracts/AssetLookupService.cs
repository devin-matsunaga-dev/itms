using System.Linq.Expressions;
using Itms.Contracts.Lookups;
using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Contracts;

/// <summary>
/// Assets' half of <see cref="IAssetLookup"/> — the only way another module reads an
/// asset (ARCHITECTURE.md §3 rule 2), and the last of the four lookup contracts to get an
/// implementation.
/// </summary>
/// <remarks>
/// <para>
/// Every query projects through the one <see cref="Projection"/>, so no method here can
/// widen what leaves the module by editing its own query — the same discipline
/// <c>UserLookupService</c> keeps for credential state. What is deliberately absent is
/// cost, vendor, notes, serial, and the warranty: none of them is something a ticket or an
/// alert header renders, and the narrow shape is what stops this becoming the asset
/// aggregate by accretion.
/// </para>
/// <para>
/// <b>Soft-deleted assets are excluded for free.</b> The global query filter on
/// <c>DeletedAt</c> applies here exactly as it does to the module's own reads, so a
/// deleted asset is invisible through the contract as well — which is what
/// <see cref="GetAsync"/>'s "or is soft-deleted" promises.
/// </para>
/// </remarks>
/// <param name="database">The assets context.</param>
internal sealed class AssetLookupService(AssetsDbContext database) : IAssetLookup
{
    /// <inheritdoc />
    public async Task<AssetSummary?> GetAsync(Guid assetId, CancellationToken cancellationToken) =>
        await database.Assets
            .AsNoTracking()
            .Where(asset => asset.Id == assetId)
            .Select(Projection())
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssetSummary>> GetManyAsync(
        IReadOnlyCollection<Guid> assetIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assetIds);

        if (assetIds.Count == 0)
        {
            return [];
        }

        // One query for a whole screen. The alternative — a lookup per row — is how a
        // timeline of twenty asset links becomes twenty round trips.
        var ids = assetIds.Distinct().ToArray();

        return await database.Assets
            .AsNoTracking()
            .Where(asset => ids.Contains(asset.Id))
            .OrderBy(asset => asset.AssetTag)
            .Select(Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Matched on the normalized tag, which is what <c>ix_assets_normalized_asset_tag</c>
    /// is unique on: a tag typed in lower case at an import prompt is the same tag as the
    /// one on the label, and a lookup that missed it would silently create a second asset
    /// for one machine.
    /// </remarks>
    public async Task<AssetSummary?> FindByTagAsync(string assetTag, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetTag))
        {
            return null;
        }

        // Trimmed here rather than through AssetTagRules.Clean: a caller asking after a
        // tag that could never have been stored wants an empty answer, not the exception
        // Clean throws for a malformed one.
        var normalized = AssetTagRules.Normalize(assetTag.Trim());

        return await database.Assets
            .AsNoTracking()
            .Where(asset => asset.NormalizedAssetTag == normalized)
            .Select(Projection())
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Ordered by tag so the panel is stable between reads, and the first reader of
    /// <c>ix_assets_assigned_to_user_id</c>, which WP-2.1 added and WP-2.2 started filling.
    /// Unpaged, because this answers "what is this person holding" — a handful of things,
    /// not a queue.
    /// </remarks>
    public async Task<IReadOnlyList<AssetSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await database.Assets
            .AsNoTracking()
            .Where(asset => asset.AssignedToUserId == userId)
            .OrderBy(asset => asset.AssetTag)
            .Select(Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// The one shape every read here projects to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The type's name and the status's <em>code</em> come from correlated subqueries
    /// rather than a second round trip. The code and not the name, because
    /// <see cref="AssetSummary.Status"/> is what a consumer branches and colours on and
    /// WP-2.1 gave a status an immutable code precisely so a rename cannot move that key;
    /// the name is Assets' own screen's business. Both foreign keys are <c>NOT NULL</c>
    /// with <c>ON DELETE RESTRICT</c>, so neither subquery can come back empty.
    /// </para>
    /// <para>
    /// The display name falls back to the asset tag, never to null: the contract promises
    /// a name "suitable for a ticket or alert header", and the tag is what is printed on
    /// the label somebody is looking at. An instance method rather than a static one only
    /// because it closes over the context to write the subqueries.
    /// </para>
    /// </remarks>
    private Expression<Func<Asset, AssetSummary>> Projection() =>
        asset => new AssetSummary(
            asset.Id,
            asset.AssetTag,
            asset.Name ?? asset.AssetTag,
            database.AssetTypes
                .Where(type => type.Id == asset.AssetTypeId)
                .Select(type => type.Name)
                .FirstOrDefault()!,
            database.AssetStatuses
                .Where(status => status.Id == asset.AssetStatusId)
                .Select(status => status.Code)
                .FirstOrDefault()!,
            asset.AssignedToUserId,
            asset.LocationId);
}
