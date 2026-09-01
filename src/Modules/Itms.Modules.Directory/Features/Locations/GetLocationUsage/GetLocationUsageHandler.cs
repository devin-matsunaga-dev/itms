using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Features.Usage;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Features.Locations.GetLocationUsage;

/// <summary>
/// Reports what a location still holds, so a delete is offered against a number rather
/// than against a hope.
/// </summary>
/// <remarks>
/// This is the read half of WP-2.4's "usage counts before deletion". The write half is in
/// <c>DeleteLocationHandler</c>, which asks the same question again at the moment of the
/// delete — this answer is a screen's worth of context, not a lock.
/// </remarks>
/// <param name="database">The directory context.</param>
/// <param name="usage">The cross-module reference counters.</param>
internal sealed class GetLocationUsageHandler(DirectoryDbContext database, DirectoryUsageReader usage)
{
    /// <summary>Reports on the location with <paramref name="locationId"/>.</summary>
    /// <param name="locationId">The node to report on.</param>
    /// <param name="cancellationToken">Cancels the counts.</param>
    /// <returns>The usage report, or a not-found failure.</returns>
    public async Task<Result<LocationUsageResponse>> HandleAsync(Guid locationId, CancellationToken cancellationToken)
    {
        var location = await database.Locations
            .AsNoTracking()
            .Where(candidate => candidate.Id == locationId)
            .Select(candidate => new { candidate.Name, candidate.FullPath })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (location is null)
        {
            return DirectoryErrors.LocationNotFound();
        }

        var children = await database.Locations
            .AsNoTracking()
            .CountAsync(child => child.ParentId == locationId, cancellationToken)
            .ConfigureAwait(false);

        var (references, total) = await usage.ForLocationAsync(locationId, cancellationToken).ConfigureAwait(false);

        return new LocationUsageResponse(
            locationId,
            location.Name,
            location.FullPath,
            children,
            references,
            total,
            CanDelete: children == 0 && total == 0);
    }
}
