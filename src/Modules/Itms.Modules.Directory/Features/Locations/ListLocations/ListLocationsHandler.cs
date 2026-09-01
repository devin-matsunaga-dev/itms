using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Features.Locations.ListLocations;

/// <summary>Lists locations as a flat, path-ordered page.</summary>
/// <remarks>
/// Flat rather than nested: ordering by the materialised display path puts every child
/// directly under its parent, so a client can render a tree from this without the API
/// having to serialise one — and paging still means something, which it would not on a
/// nested document.
/// </remarks>
/// <param name="database">The directory context.</param>
internal sealed class ListLocationsHandler(DirectoryDbContext database)
{
    /// <summary>Reads a page of locations.</summary>
    /// <param name="search">A fragment matched against the node's name and its full path, or <see langword="null"/>.</param>
    /// <param name="parentId">Only direct children of this node, or <see langword="null"/> for no parent filter.</param>
    /// <param name="rootId">Only this node and everything beneath it, or <see langword="null"/> for the whole tree.</param>
    /// <param name="kind">Only nodes of this kind, or <see langword="null"/> for all kinds.</param>
    /// <param name="adoptableFor">
    /// Only nodes that could legally be the parent of a location of this kind, or
    /// <see langword="null"/> for no such filter. This is the filter a cascading picker
    /// uses so it never offers a parent the server would refuse.
    /// </param>
    /// <param name="page">The page to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The page envelope, or a not-found when <paramref name="rootId"/> does not exist.</returns>
    public async Task<Result<PagedResult<LocationResponse>>> HandleAsync(
        string? search,
        Guid? parentId,
        Guid? rootId,
        LocationKind? kind,
        LocationKind? adoptableFor,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = database.Locations.AsNoTracking();

        if (rootId is { } root)
        {
            var rootPath = await database.Locations
                .AsNoTracking()
                .Where(candidate => candidate.Id == root)
                .Select(candidate => candidate.Path)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (rootPath is null)
            {
                return DirectoryErrors.LocationNotFound();
            }

            // The subtree, in one prefix match rather than a recursive walk.
            var subtree = SearchPattern.StartingWith(rootPath);
            query = query.Where(candidate => EF.Functions.Like(candidate.Path, subtree));
        }

        if (parentId is { } parent)
        {
            query = query.Where(candidate => candidate.ParentId == parent);
        }

        if (kind is { } wanted)
        {
            query = query.Where(candidate => candidate.Kind == wanted);
        }

        if (adoptableFor is { } childKind)
        {
            query = LocationAdoption.Filter(query, childKind);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = SearchPattern.Containing(search);
            query = query.Where(candidate =>
                EF.Functions.ILike(candidate.Name, pattern, SearchPattern.Escape) ||
                EF.Functions.ILike(candidate.FullPath, pattern, SearchPattern.Escape));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderBy(candidate => candidate.FullPath)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(LocationQueries.Projection(database))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.From<LocationResponse>(items, total, page);
    }
}
