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

namespace Itms.Modules.Directory.Features.Locations.UpdateLocation;

/// <summary>
/// Renames a location and rewrites the display path of everything beneath it.
/// </summary>
/// <remarks>
/// The rewrite is what pays for the materialised path. It happens in the same
/// transaction as the rename, so a crash cannot leave half a subtree claiming to live
/// under a name that no longer exists.
/// </remarks>
/// <param name="database">The directory context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
/// <param name="logger">Structured log.</param>
internal sealed class UpdateLocationHandler(
    DirectoryDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit,
    ILogger<UpdateLocationHandler> logger)
{
    /// <summary>Applies <paramref name="request"/> to the location with <paramref name="locationId"/>.</summary>
    /// <param name="locationId">The node to edit.</param>
    /// <param name="request">The new name and description.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The edited location, a not-found, or a conflict on a duplicate sibling name.</returns>
    public async Task<Result<LocationResponse>> HandleAsync(
        Guid locationId,
        UpdateLocationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Error? failure = null;
        LocationResponse? updated = null;

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

                var parentFullPath = location.ParentId is { } parentId
                    ? await database.Locations
                        .AsNoTracking()
                        .Where(candidate => candidate.Id == parentId)
                        .Select(candidate => candidate.FullPath)
                        .FirstAsync(token)
                        .ConfigureAwait(false)
                    : null;

                // Read before the entity mutates; after Rename there is nothing left to
                // compare the new values against.
                var previousName = location.Name;
                var previousDescription = location.Description;

                var now = clock.UtcNow;
                var actor = currentUser.UserId;

                var rewrite = location.Rename(request.Name, parentFullPath, now, actor);
                location.Describe(request.Description, now, actor);

                failure = await LocationUniqueness
                    .CheckAsync(database, location.ParentId, location.NormalizedName, locationId, token)
                    .ConfigureAwait(false);

                if (failure is not null)
                {
                    return;
                }

                var rewritten = await LocationQueries
                    .RewriteSubtreeAsync(database, locationId, rewrite, now, actor, token)
                    .ConfigureAwait(false);

                await database.SaveChangesAsync(token).ConfigureAwait(false);

                if (rewritten > 0)
                {
                    DirectoryLog.SubtreeRewritten(logger, locationId, rewritten);
                }

                await audit.WriteAsync(
                    new AuditEntry(
                        DirectoryAudit.LocationUpdated,
                        DirectoryAudit.LocationEntityType,
                        location.Id.ToString(),
                        DirectoryAudit.Changes()
                            .Moved("name", previousName, location.Name)
                            .Moved("description", previousDescription, location.Description)),
                    token).ConfigureAwait(false);

                var childCount = await database.Locations
                    .CountAsync(child => child.ParentId == locationId, token)
                    .ConfigureAwait(false);

                updated = new LocationResponse(
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

        return failure is not null ? failure : updated!;
    }
}
