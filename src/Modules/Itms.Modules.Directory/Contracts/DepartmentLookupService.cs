using Itms.Contracts.Lookups;
using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Contracts;

/// <summary>
/// Directory's half of <see cref="IDepartmentLookup"/> — the only way another module
/// reads a department (ARCHITECTURE.md §3 rule 2).
/// </summary>
/// <remarks>
/// Retired departments are returned rather than hidden, with <c>IsActive</c> false. A
/// ticket raised two years ago against a department that no longer exists still has to
/// render, and a lookup that returned null for it would leave the caller with an id and
/// nothing to show.
/// </remarks>
/// <param name="database">The directory context.</param>
internal sealed class DepartmentLookupService(DirectoryDbContext database) : IDepartmentLookup
{
    /// <inheritdoc />
    public async Task<DepartmentSummary?> GetAsync(Guid departmentId, CancellationToken cancellationToken) =>
        await database.Departments
            .AsNoTracking()
            .Where(department => department.Id == departmentId)
            .Select(Projection())
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<DepartmentSummary>> GetManyAsync(
        IReadOnlyCollection<Guid> departmentIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(departmentIds);

        if (departmentIds.Count == 0)
        {
            return [];
        }

        // One query for a whole list screen. The alternative — a lookup per row — is how
        // a ticket list becomes fifty round trips.
        var ids = departmentIds.Distinct().ToArray();

        return await database.Departments
            .AsNoTracking()
            .Where(department => ids.Contains(department.Id))
            .Select(Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static System.Linq.Expressions.Expression<Func<Department, DepartmentSummary>> Projection() =>
        department => new DepartmentSummary(department.Id, department.Name, department.IsActive);
}
