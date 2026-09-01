using Itms.Contracts.Auditing;
using Itms.Modules.Directory.Auditing;
using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Features.Usage;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Itms.Modules.Directory.Features.Locations.DeleteLocation;

/// <summary>
/// Deletes a leaf of the location tree.
/// </summary>
/// <remarks>
/// <para>
/// A node with children is refused with a 409 naming how many, rather than being deleted
/// with its subtree. Cascading here would let one mistaken click remove a site and every
/// room under it — and the assets and alerts referencing those rooms hold plain
/// identifiers with no foreign key to stop it (§3 rule 6). The foreign key on
/// <c>parent_id</c> is <c>Restrict</c> for the same reason, so the database refuses it
/// too if a future code path ever forgets to ask.
/// </para>
/// <para>
/// A leaf that other modules still reference is refused as well (WP-2.4). The same rule 6
/// that stops the database cascading also stops it noticing: an asset's <c>location_id</c>
/// is a plain identifier, so deleting the room it names leaves a row pointing at nothing,
/// with no error anywhere and a blank cell on a screen months later. The counts come from
/// the modules themselves through <see cref="Itms.Contracts.Lookups.IDirectoryUsageLookup"/>,
/// and they are read inside this transaction rather than trusted from the caller's earlier
/// <c>GET /locations/{id}/usage</c> — which is a screen's context, not a reservation.
/// </para>
/// </remarks>
/// <param name="database">The directory context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="usage">The cross-module reference counters.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
/// <param name="logger">Structured log.</param>
internal sealed class DeleteLocationHandler(
    DirectoryDbContext database,
    IModuleDbSession session,
    DirectoryUsageReader usage,
    IAuditWriter audit,
    ILogger<DeleteLocationHandler> logger)
{
    /// <summary>Deletes the location with <paramref name="locationId"/>.</summary>
    /// <param name="locationId">The node to delete.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success, a not-found, or a conflict naming the children that block it.</returns>
    public async Task<Result> HandleAsync(Guid locationId, CancellationToken cancellationToken)
    {
        Error? failure = null;

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

                var children = await database.Locations
                    .CountAsync(child => child.ParentId == locationId, token)
                    .ConfigureAwait(false);

                if (children > 0)
                {
                    failure = DirectoryErrors.LocationHasChildren(location.Name, children);
                    return;
                }

                var (references, referenceTotal) = await usage
                    .ForLocationAsync(locationId, token)
                    .ConfigureAwait(false);

                if (referenceTotal > 0)
                {
                    failure = DirectoryErrors.LocationInUse(
                        location.Name,
                        UsageBreakdown.Describe(references));

                    return;
                }

                var name = location.Name;
                var kind = location.Kind;
                var fullPath = location.FullPath;

                database.Locations.Remove(location);
                await database.SaveChangesAsync(token).ConfigureAwait(false);

                DirectoryLog.LocationDeleted(logger, locationId);

                // Value-to-null, because the row is gone: the entry is the only remaining
                // record of what was there, and the assets and alerts that referenced this
                // id hold no foreign key that could tell anyone afterwards.
                await audit.WriteAsync(
                    new AuditEntry(
                        DirectoryAudit.LocationDeleted,
                        DirectoryAudit.LocationEntityType,
                        locationId.ToString(),
                        DirectoryAudit.Changes()
                            .Moved("name", name, null)
                            .Moved("kind", kind.ToString(), null)
                            .Moved("fullPath", fullPath, null)),
                    token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is null ? Result.Success() : Result.Failure(failure);
    }
}
