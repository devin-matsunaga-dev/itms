using Itms.Modules.Directory.Domain;
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
/// A node with children is refused with a 409 naming how many, rather than being deleted
/// with its subtree. Cascading here would let one mistaken click remove a site and every
/// room under it — and the assets and alerts referencing those rooms hold plain
/// identifiers with no foreign key to stop it (§3 rule 6). The foreign key on
/// <c>parent_id</c> is <c>Restrict</c> for the same reason, so the database refuses it
/// too if a future code path ever forgets to ask.
/// </remarks>
/// <param name="database">The directory context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="logger">Structured log.</param>
internal sealed class DeleteLocationHandler(
    DirectoryDbContext database,
    IModuleDbSession session,
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

                database.Locations.Remove(location);
                await database.SaveChangesAsync(token).ConfigureAwait(false);

                DirectoryLog.LocationDeleted(logger, locationId);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is null ? Result.Success() : Result.Failure(failure);
    }
}
