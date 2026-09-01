using Itms.Modules.Assets.Persistence;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.AssetStatuses.ListAssetStatuses;

/// <summary>Lists asset statuses in picker order.</summary>
/// <param name="database">The assets context.</param>
internal sealed class ListAssetStatusesHandler(AssetsDbContext database)
{
    /// <summary>Reads a page of statuses.</summary>
    /// <param name="includeInactive">True to include retired statuses.</param>
    /// <param name="page">The page to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The page envelope.</returns>
    public async Task<Result<PagedResult<AssetStatusResponse>>> HandleAsync(
        bool includeInactive,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = database.AssetStatuses.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(status => status.IsActive);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Sort order first, then name: sort_order is not unique, and a picker whose order
        // changes between two reads of the same data is a bug nobody can reproduce.
        var items = await query
            .OrderBy(status => status.SortOrder)
            .ThenBy(status => status.NormalizedName)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(AssetStatusResponse.Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.From<AssetStatusResponse>(items, total, page);
    }
}
