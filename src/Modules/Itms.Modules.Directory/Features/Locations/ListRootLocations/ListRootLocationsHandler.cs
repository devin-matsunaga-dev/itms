using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Features.Locations.ListRootLocations;

/// <summary>
/// Lists the top level of the tree — the first select of a cascading picker.
/// </summary>
/// <remarks>
/// A separate route rather than a value on <c>GET /locations</c>, because
/// <c>?parentId=</c> there means "no parent filter" and has meant that since WP-0.6. A
/// tri-state query parameter that distinguished "unset" from "explicitly null" would put
/// the difference between "the whole tree" and "the roots" into whether a client sent an
/// empty string — which is exactly the sort of thing that works until a proxy trims it.
/// </remarks>
/// <param name="database">The directory context.</param>
internal sealed class ListRootLocationsHandler(DirectoryDbContext database)
{
    /// <summary>Reads a page of root locations.</summary>
    /// <param name="search">A fragment matched against the node's name, or <see langword="null"/>.</param>
    /// <param name="adoptableFor">
    /// Only roots that could hold a node of this kind, or <see langword="null"/> for all
    /// of them. A root is an Organization, so this is in practice a depth check.
    /// </param>
    /// <param name="page">The page to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The page envelope.</returns>
    public async Task<Result<PagedResult<LocationResponse>>> HandleAsync(
        string? search,
        LocationKind? adoptableFor,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = database.Locations.AsNoTracking().Where(candidate => candidate.ParentId == null);

        if (adoptableFor is { } childKind)
        {
            query = LocationAdoption.Filter(query, childKind);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = SearchPattern.Containing(search);
            query = query.Where(candidate => EF.Functions.ILike(candidate.Name, pattern, SearchPattern.Escape));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderBy(candidate => candidate.NormalizedName)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(LocationQueries.Projection(database))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.From<LocationResponse>(items, total, page);
    }
}
