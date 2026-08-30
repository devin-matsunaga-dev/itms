using Itms.Contracts.Lookups;
using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Contracts;

/// <summary>
/// Directory's half of <see cref="ILocationLookup"/> — the only way another module reads
/// a location (ARCHITECTURE.md §3 rule 2).
/// </summary>
/// <remarks>
/// <c>LocationSummary.Path</c> is served straight out of the materialised
/// <see cref="Location.FullPath"/> column, so an alert copying its location context
/// costs one row read and never walks the tree. That is the whole reason the column
/// exists: invariant 7 requires an alert to keep the location context it was raised
/// with, and a caller that had to assemble the path itself would be tempted to store an
/// id instead.
/// </remarks>
/// <param name="database">The directory context.</param>
internal sealed class LocationLookupService(DirectoryDbContext database) : ILocationLookup
{
    /// <inheritdoc />
    public async Task<LocationSummary?> GetAsync(Guid locationId, CancellationToken cancellationToken) =>
        await database.Locations
            .AsNoTracking()
            .Where(location => location.Id == locationId)
            .Select(Projection())
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<LocationSummary>> GetManyAsync(
        IReadOnlyCollection<Guid> locationIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(locationIds);

        if (locationIds.Count == 0)
        {
            return [];
        }

        var ids = locationIds.Distinct().ToArray();

        return await database.Locations
            .AsNoTracking()
            .Where(location => ids.Contains(location.Id))
            .Select(Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static System.Linq.Expressions.Expression<Func<Location, LocationSummary>> Projection() =>
        location => new LocationSummary(location.Id, location.Name, location.FullPath, location.ParentId);
}
