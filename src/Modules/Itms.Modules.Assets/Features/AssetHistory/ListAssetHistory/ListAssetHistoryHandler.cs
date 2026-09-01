using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.AssetHistory.ListAssetHistory;

/// <summary>Reads one asset's timeline, newest first.</summary>
/// <remarks>
/// <para>
/// Technician-or-Admin, like every other route on the asset surface: SPEC.md §14 puts the
/// inventory on the operational surface, and an asset has no requester-scoped reading of
/// the kind a ticket has. The "who has had this equipment" question an end user might ask
/// about their own kit is WP-2.5's user page, which answers it through a different route.
/// </para>
/// <para>
/// A read endpoint is in this package rather than left to WP-2.3 for the reason WP-2.1
/// pulled <c>GET /assets/{id}</c> forward: a history table with no route to read it cannot
/// be verified by a human, and WP-2.6 would have to invent the shape while building the
/// screen that renders it.
/// </para>
/// </remarks>
/// <param name="database">The assets context.</param>
public sealed class ListAssetHistoryHandler(AssetsDbContext database)
{
    /// <summary>Reads a page of the timeline.</summary>
    /// <param name="assetId">The asset whose history is wanted.</param>
    /// <param name="page">The page to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The page envelope, or a not-found failure when there is no such asset.</returns>
    public async Task<Result<PagedResult<AssetHistoryEntryResponse>>> HandleAsync(
        Guid assetId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        // Asked first, and against the asset rather than the history, so an asset that
        // exists and has not moved yet answers with an empty page while one that does not
        // exist answers 404. Reading the history alone could not tell those apart, and an
        // empty timeline for an asset tag nobody has is the more misleading of the two.
        // The soft-delete filter applies, so a deleted asset is a 404 here too.
        var exists = await database.Assets
            .AsNoTracking()
            .AnyAsync(asset => asset.Id == assetId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            return Result.Failure<PagedResult<AssetHistoryEntryResponse>>(AssetsErrors.AssetNotFound());
        }

        var query = database.AssetHistory
            .AsNoTracking()
            .Where(entry => entry.AssetId == assetId);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Newest first. One operation can write more than one entry — issuing equipment out
        // of stock moves the holder and the status — and those entries share an instant
        // because they genuinely happened at one, so the ordinal within the operation is
        // what orders them. The id is a last tiebreaker only, for two operations landing on
        // the same instant; without all three, a timeline could come back in a different
        // order on the second read of the same data, which is what makes a paged list
        // unusable.
        var items = await query
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Sequence)
            .ThenByDescending(entry => entry.Id)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(AssetHistoryEntryResponse.Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.From<AssetHistoryEntryResponse>(items, total, page);
    }
}
