using System.Diagnostics.CodeAnalysis;
using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Data;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Features.Locations;

/// <summary>The location projection and the subtree rewrite every slice shares.</summary>
internal static class LocationQueries
{
    /// <summary>
    /// Projects a location to its API shape, counting its children in the same query.
    /// </summary>
    /// <param name="database">The context, so the child count becomes a correlated subquery rather than a second round trip.</param>
    /// <returns>The projection.</returns>
    public static System.Linq.Expressions.Expression<Func<Location, LocationResponse>> Projection(
        DirectoryDbContext database) =>
        location => new LocationResponse(
            location.Id,
            location.Name,
            location.Kind,
            location.ParentId,
            location.FullPath,
            location.Depth,
            location.Description,
            database.Locations.Count(child => child.ParentId == location.Id),
            location.CreatedAt,
            location.UpdatedAt);

    /// <summary>
    /// Applies a rename or a move to everything beneath the node that changed.
    /// </summary>
    /// <param name="database">The directory context, already enlisted in the caller's transaction.</param>
    /// <param name="nodeId">The node that changed. It is excluded — the change tracker owns its row.</param>
    /// <param name="rewrite">What the node's own change did to it.</param>
    /// <param name="now">The current instant, written to every touched row.</param>
    /// <param name="actor">Who caused the rewrite.</param>
    /// <param name="cancellationToken">Cancels the update.</param>
    /// <returns>How many descendants were rewritten.</returns>
    /// <remarks>
    /// <para>
    /// One <c>UPDATE</c> over a prefix match on the materialised id path, not a recursive
    /// walk. Renaming a site with four hundred rooms under it is one statement, and the
    /// pattern-ops index on <c>path</c> is what keeps it from being a sequential scan.
    /// </para>
    /// <para>
    /// It reads the paths as they still stand in the database, which is why the node's own
    /// pending change does not interfere: its new values live in the change tracker until
    /// <c>SaveChanges</c>, and its row is excluded here regardless.
    /// </para>
    /// </remarks>
    [SuppressMessage(
        "Performance",
        "CA1845:Use span-based 'string.Concat' and 'AsSpan' instead of 'Substring'",
        Justification = "The Substring calls are inside an expression tree that EF Core translates to SQL. " +
                        "There is no span in PostgreSQL, and AsSpan has no translation, so taking the analyzer's " +
                        "advice would turn the whole UPDATE into client-side evaluation.")]
    public static async Task<int> RewriteSubtreeAsync(
        DirectoryDbContext database,
        Guid nodeId,
        SubtreeRewrite rewrite,
        DateTimeOffset now,
        Guid? actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (rewrite.IsNoop)
        {
            return 0;
        }

        // Hoisted out of the expression tree: EF parameterises locals cleanly, whereas
        // repeated struct member access inside the tree is translated far less predictably.
        var subtree = SearchPattern.StartingWith(rewrite.OldPath);
        var newPath = rewrite.NewPath;
        var newFullPath = rewrite.NewFullPath;
        var oldPathLength = rewrite.OldPath.Length;
        var oldFullPathLength = rewrite.OldFullPath.Length;
        var depthShift = rewrite.DepthShift;

        return await database.Locations
            .Where(descendant => descendant.Id != nodeId && EF.Functions.Like(descendant.Path, subtree))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(descendant => descendant.Path, descendant => newPath + descendant.Path.Substring(oldPathLength))
                    .SetProperty(descendant => descendant.FullPath, descendant => newFullPath + descendant.FullPath.Substring(oldFullPathLength))
                    .SetProperty(descendant => descendant.Depth, descendant => descendant.Depth + depthShift)
                    .SetProperty(descendant => descendant.UpdatedAt, now)
                    .SetProperty(descendant => descendant.UpdatedBy, actor),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
