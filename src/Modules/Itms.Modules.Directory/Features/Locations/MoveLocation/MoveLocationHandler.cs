using Itms.Contracts.Auditing;
using Itms.Modules.Directory.Auditing;
using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Itms.Modules.Directory.Features.Locations.MoveLocation;

/// <summary>
/// Reparents a location, carrying its whole subtree with it.
/// </summary>
/// <remarks>
/// Buildings get renumbered and departments get moved between sites, so a directory
/// that could only be built and never rearranged would be re-entered by hand within a
/// year. The three refusals — a cycle, an inverted hierarchy, and a subtree that would
/// run past the depth limit — are all checked before anything is written.
/// </remarks>
/// <param name="database">The directory context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
/// <param name="logger">Structured log.</param>
internal sealed class MoveLocationHandler(
    DirectoryDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit,
    ILogger<MoveLocationHandler> logger)
{
    /// <summary>Moves the location with <paramref name="locationId"/> under a new parent.</summary>
    /// <param name="locationId">The node to move.</param>
    /// <param name="request">The new parent, or null for the root.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The moved location, or a failure describing why the move was refused.</returns>
    public async Task<Result<LocationResponse>> HandleAsync(
        Guid locationId,
        MoveLocationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Error? failure = null;
        LocationResponse? moved = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                var location = await database.Locations
                    .FirstOrDefaultAsync(candidate => candidate.Id == locationId, token)
                    .ConfigureAwait(false);

                if (location is null)
                {
                    failure = DirectoryErrors.LocationNotFound();
                    return;
                }

                Location? parent = null;

                if (request.ParentId is { } parentId)
                {
                    parent = await database.Locations
                        .FirstOrDefaultAsync(candidate => candidate.Id == parentId, token)
                        .ConfigureAwait(false);

                    if (parent is null)
                    {
                        failure = DirectoryErrors.ParentNotFound();
                        return;
                    }

                    // Answered from the materialised path, so the cycle check is a string
                    // comparison rather than a walk back up the tree.
                    if (parent.Id == location.Id || location.IsAncestorOf(parent))
                    {
                        failure = DirectoryErrors.WouldCreateCycle();
                        return;
                    }

                    if (!LocationHierarchy.CanContain(parent.Kind, location.Kind))
                    {
                        failure = DirectoryErrors.IllegalPlacement(parent.Kind, location.Kind);
                        return;
                    }
                }
                else if (!LocationHierarchy.CanBeRoot(location.Kind))
                {
                    failure = DirectoryErrors.RootMustBeOrganization(location.Kind);
                    return;
                }

                failure = await CheckDepthAsync(location, parent, token).ConfigureAwait(false);
                if (failure is not null)
                {
                    return;
                }

                failure = await LocationUniqueness
                    .CheckAsync(database, parent?.Id, location.NormalizedName, locationId, token)
                    .ConfigureAwait(false);

                if (failure is not null)
                {
                    return;
                }

                var previousParentId = location.ParentId;
                var previousFullPath = location.FullPath;

                var now = clock.UtcNow;
                var actor = currentUser.UserId;

                var rewrite = location.MoveTo(parent, now, actor);

                var rewritten = await LocationQueries
                    .RewriteSubtreeAsync(database, locationId, rewrite, now, actor, token)
                    .ConfigureAwait(false);

                await database.SaveChangesAsync(token).ConfigureAwait(false);

                DirectoryLog.LocationMoved(logger, locationId, parent?.Id, rewritten);

                await audit.WriteAsync(
                    new AuditEntry(
                        DirectoryAudit.LocationMoved,
                        DirectoryAudit.LocationEntityType,
                        location.Id.ToString(),
                        DirectoryAudit.Changes()
                            .Moved("parentId", previousParentId?.ToString(), location.ParentId?.ToString())
                            .Moved("fullPath", previousFullPath, location.FullPath)),
                    token).ConfigureAwait(false);

                var childCount = await database.Locations
                    .CountAsync(child => child.ParentId == locationId, token)
                    .ConfigureAwait(false);

                moved = new LocationResponse(
                    location.Id,
                    location.Name,
                    location.Kind,
                    location.ParentId,
                    location.FullPath,
                    location.Depth,
                    location.Description,
                    childCount,
                    location.CreatedAt,
                    location.UpdatedAt);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? failure : moved!;
    }

    /// <summary>
    /// Refuses a move whose deepest descendant would end up past the depth limit.
    /// </summary>
    /// <remarks>
    /// The entity checks only its own new depth, because it cannot see the subtree. The
    /// limit exists to keep the materialised path inside its column, so it is the deepest
    /// descendant that has to satisfy it, not the node being moved.
    /// </remarks>
    private async Task<Error?> CheckDepthAsync(Location location, Location? parent, CancellationToken cancellationToken)
    {
        var newDepth = parent is null ? 0 : parent.Depth + 1;
        var shift = newDepth - location.Depth;

        var subtree = SearchPattern.StartingWith(location.Path);
        var deepest = await database.Locations
            .AsNoTracking()
            .Where(descendant => EF.Functions.Like(descendant.Path, subtree))
            .MaxAsync(descendant => (int?)descendant.Depth, cancellationToken)
            .ConfigureAwait(false) ?? location.Depth;

        return deepest + shift >= LocationHierarchy.MaxDepth ? DirectoryErrors.TooDeep() : null;
    }
}
