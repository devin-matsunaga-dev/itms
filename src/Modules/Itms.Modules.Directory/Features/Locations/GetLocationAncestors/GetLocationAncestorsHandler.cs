using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Features.Locations.GetLocationAncestors;

/// <summary>
/// Reads the root-to-node chain, which is what a cascading picker needs to render itself
/// around a value it was handed.
/// </summary>
/// <remarks>
/// <para>
/// A picker opened on an asset that already sits in a room has to populate five selects —
/// organisation, site, building, floor, room — and know which option is chosen in each.
/// Without this it walks <c>parentId</c> upwards, one request per level, and the deepest
/// node costs the most requests. Here it is two queries regardless of depth: the node's
/// own row, and its ancestors by primary key.
/// </para>
/// <para>
/// The chain comes out of the materialised <c>path</c> column rather than a recursive CTE
/// — the ids are already in the row, so reading them is parsing rather than querying.
/// </para>
/// </remarks>
/// <param name="database">The directory context.</param>
internal sealed class GetLocationAncestorsHandler(DirectoryDbContext database)
{
    /// <summary>Reads the chain from the root down to <paramref name="locationId"/>.</summary>
    /// <param name="locationId">The node at the end of the chain.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>
    /// The chain root first and including the node itself, or a not-found failure. A root
    /// node's chain is itself alone; it is never empty.
    /// </returns>
    public async Task<Result<IReadOnlyList<LocationResponse>>> HandleAsync(
        Guid locationId,
        CancellationToken cancellationToken)
    {
        var path = await database.Locations
            .AsNoTracking()
            .Where(candidate => candidate.Id == locationId)
            .Select(candidate => candidate.Path)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (path is null)
        {
            return DirectoryErrors.LocationNotFound();
        }

        var ids = LocationPath.ParseIds(path).ToArray();

        var chain = await database.Locations
            .AsNoTracking()
            .Where(candidate => ids.Contains(candidate.Id))
            // By depth, not by the order of the id list: the path is written root-first,
            // and depth says the same thing in a form the database can sort on.
            .OrderBy(candidate => candidate.Depth)
            .Select(LocationQueries.Projection(database))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return chain;
    }
}
