using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Features.Locations.GetLocation;

/// <summary>Reads one location, full display path included.</summary>
/// <remarks>
/// This is the query WP-0.6's "queries a full path efficiently" is about: the path comes
/// out of the row's own materialised column, so reading a room five levels deep is one
/// indexed row read and never five.
/// </remarks>
/// <param name="database">The directory context.</param>
internal sealed class GetLocationHandler(DirectoryDbContext database)
{
    /// <summary>Reads the location with <paramref name="locationId"/>.</summary>
    /// <param name="locationId">The node to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The location, or a not-found failure.</returns>
    public async Task<Result<LocationResponse>> HandleAsync(Guid locationId, CancellationToken cancellationToken)
    {
        var location = await database.Locations
            .AsNoTracking()
            .Where(candidate => candidate.Id == locationId)
            .Select(LocationQueries.Projection(database))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return location is null ? DirectoryErrors.LocationNotFound() : location;
    }
}
