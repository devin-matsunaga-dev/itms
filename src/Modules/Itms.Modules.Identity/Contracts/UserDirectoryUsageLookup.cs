using Itms.Contracts.Lookups;
using Itms.Modules.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Identity.Contracts;

/// <summary>
/// Identity's half of <see cref="IDirectoryUsageLookup"/>: how many accounts sit in a
/// department or at a location.
/// </summary>
/// <remarks>
/// Deactivated accounts are counted. Invariant 9 keeps a deactivated user's record and
/// everything hanging off it, so the row is still read and still shows the department and
/// room it points at; excluding them would let an administrator delete a location that a
/// hundred retained records name.
/// </remarks>
/// <param name="database">The identity context.</param>
internal sealed class UserDirectoryUsageLookup(ItmsIdentityDbContext database) : IDirectoryUsageLookup
{
    /// <summary>What this counter reports, as it is rendered to an administrator.</summary>
    public const string EntityName = "users";

    /// <inheritdoc />
    public async Task<DirectoryUsage> CountForDepartmentAsync(Guid departmentId, CancellationToken cancellationToken) =>
        new(EntityName, await database.Users
            .AsNoTracking()
            .CountAsync(user => user.DepartmentId == departmentId, cancellationToken)
            .ConfigureAwait(false));

    /// <inheritdoc />
    public async Task<DirectoryUsage> CountForLocationAsync(Guid locationId, CancellationToken cancellationToken) =>
        new(EntityName, await database.Users
            .AsNoTracking()
            .CountAsync(user => user.LocationId == locationId, cancellationToken)
            .ConfigureAwait(false));
}
