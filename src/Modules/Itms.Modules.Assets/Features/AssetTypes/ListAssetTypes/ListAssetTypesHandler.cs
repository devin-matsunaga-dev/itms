using Itms.Modules.Assets.Persistence;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.AssetTypes.ListAssetTypes;

/// <summary>Lists asset types in picker order.</summary>
/// <param name="database">The assets context.</param>
internal sealed class ListAssetTypesHandler(AssetsDbContext database)
{
    /// <summary>Reads a page of types.</summary>
    /// <param name="includeInactive">True to include retired types.</param>
    /// <param name="page">The page to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The page envelope.</returns>
    public async Task<Result<PagedResult<AssetTypeResponse>>> HandleAsync(
        bool includeInactive,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = database.AssetTypes.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(type => type.IsActive);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Sort order first, then name: sort_order is not unique, and a picker whose order
        // changes between two reads of the same data is a bug nobody can reproduce.
        // Projected in the query, never by loading entities and mapping them after
        // (CONVENTIONS.md).
        var items = await query
            .OrderBy(type => type.SortOrder)
            .ThenBy(type => type.NormalizedName)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(AssetTypeResponse.Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.From<AssetTypeResponse>(items, total, page);
    }
}
