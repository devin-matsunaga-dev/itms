using Itms.Contracts.Lookups;
using Itms.Modules.Assets.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Contracts;

/// <summary>
/// Assets' half of <see cref="IDirectoryUsageLookup"/>: how much equipment a department
/// or a room still holds.
/// </summary>
/// <remarks>
/// Soft-deleted assets are excluded, and they are excluded for free — the global query
/// filter on <c>DeletedAt</c> applies here exactly as it does to every other asset read,
/// so a deleted asset cannot be what blocks a room from being removed.
/// </remarks>
/// <param name="database">The assets context.</param>
internal sealed class AssetDirectoryUsageLookup(AssetsDbContext database) : IDirectoryUsageLookup
{
    /// <summary>What this counter reports, as it is rendered to an administrator.</summary>
    public const string EntityName = "assets";

    /// <inheritdoc />
    public async Task<DirectoryUsage> CountForDepartmentAsync(Guid departmentId, CancellationToken cancellationToken) =>
        new(EntityName, await database.Assets
            .AsNoTracking()
            .CountAsync(asset => asset.DepartmentId == departmentId, cancellationToken)
            .ConfigureAwait(false));

    /// <inheritdoc />
    public async Task<DirectoryUsage> CountForLocationAsync(Guid locationId, CancellationToken cancellationToken) =>
        new(EntityName, await database.Assets
            .AsNoTracking()
            .CountAsync(asset => asset.LocationId == locationId, cancellationToken)
            .ConfigureAwait(false));
}
