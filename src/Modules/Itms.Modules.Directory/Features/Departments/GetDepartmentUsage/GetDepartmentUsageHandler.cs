using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Features.Usage;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Features.Departments.GetDepartmentUsage;

/// <summary>
/// Reports what a department still holds, before an administrator retires it.
/// </summary>
/// <remarks>
/// Informational only. Unlike a location, a department is never deleted and its
/// retirement is never refused — see <c>DepartmentUsageResponse</c> for why. What this
/// answers is "what am I about to affect", which is a different question from "am I
/// allowed".
/// </remarks>
/// <param name="database">The directory context.</param>
/// <param name="usage">The cross-module reference counters.</param>
internal sealed class GetDepartmentUsageHandler(DirectoryDbContext database, DirectoryUsageReader usage)
{
    /// <summary>Reports on the department with <paramref name="departmentId"/>.</summary>
    /// <param name="departmentId">The department to report on.</param>
    /// <param name="cancellationToken">Cancels the counts.</param>
    /// <returns>The usage report, or a not-found failure.</returns>
    public async Task<Result<DepartmentUsageResponse>> HandleAsync(
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        var department = await database.Departments
            .AsNoTracking()
            .Where(candidate => candidate.Id == departmentId)
            .Select(candidate => new { candidate.Name, candidate.IsActive })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (department is null)
        {
            return DirectoryErrors.DepartmentNotFound();
        }

        var (references, total) = await usage.ForDepartmentAsync(departmentId, cancellationToken).ConfigureAwait(false);

        return new DepartmentUsageResponse(departmentId, department.Name, department.IsActive, references, total);
    }
}
