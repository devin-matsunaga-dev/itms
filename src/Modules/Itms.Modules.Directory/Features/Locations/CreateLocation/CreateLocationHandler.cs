using Itms.Contracts.Auditing;
using Itms.Modules.Directory.Auditing;
using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Features.Locations.CreateLocation;

/// <summary>Creates a node in the location tree.</summary>
/// <param name="database">The directory context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
internal sealed class CreateLocationHandler(
    DirectoryDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit)
{
    /// <summary>Creates the location described by <paramref name="request"/>.</summary>
    /// <param name="request">The new node's fields.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The created location, or a failure describing why it could not be placed.</returns>
    public async Task<Result<LocationResponse>> HandleAsync(
        CreateLocationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Error? failure = null;
        LocationResponse? created = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

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

                    if (!LocationHierarchy.CanContain(parent.Kind, request.Kind))
                    {
                        failure = DirectoryErrors.IllegalPlacement(parent.Kind, request.Kind);
                        return;
                    }

                    if (!parent.CanAdopt(request.Kind))
                    {
                        failure = DirectoryErrors.TooDeep();
                        return;
                    }
                }
                else if (!LocationHierarchy.CanBeRoot(request.Kind))
                {
                    failure = DirectoryErrors.RootMustBeOrganization(request.Kind);
                    return;
                }

                var location = Location.Create(
                    parent,
                    request.Name,
                    request.Kind,
                    request.Description,
                    clock.UtcNow,
                    currentUser.UserId);

                failure = await LocationUniqueness
                    .CheckAsync(database, location.ParentId, location.NormalizedName, excluding: null, token)
                    .ConfigureAwait(false);

                if (failure is not null)
                {
                    return;
                }

                database.Locations.Add(location);
                await database.SaveChangesAsync(token).ConfigureAwait(false);

                await audit.WriteAsync(
                    new AuditEntry(
                        DirectoryAudit.LocationCreated,
                        DirectoryAudit.LocationEntityType,
                        location.Id.ToString(),
                        DirectoryAudit.Changes()
                            .Set("name", location.Name)
                            .Set("kind", location.Kind.ToString())
                            .Set("parentId", location.ParentId?.ToString())
                            // The display path, so the entry still says where the node was
                            // put after an ancestor is later renamed or moved.
                            .Set("fullPath", location.FullPath)
                            .Set("description", location.Description)),
                    token).ConfigureAwait(false);

                created = new LocationResponse(
                    location.Id,
                    location.Name,
                    location.Kind,
                    location.ParentId,
                    location.FullPath,
                    location.Depth,
                    location.Description,
                    ChildCount: 0,
                    location.CreatedAt,
                    location.UpdatedAt);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? failure : created!;
    }
}
