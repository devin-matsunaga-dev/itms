using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Features.Departments.GetDepartment;

/// <summary>Reads one department.</summary>
/// <param name="database">The directory context.</param>
internal sealed class GetDepartmentHandler(DirectoryDbContext database)
{
    /// <summary>Reads the department with <paramref name="departmentId"/>.</summary>
    /// <param name="departmentId">The department to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The department, or a not-found failure.</returns>
    public async Task<Result<DepartmentResponse>> HandleAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var department = await database.Departments
            .AsNoTracking()
            .Where(candidate => candidate.Id == departmentId)
            .Select(DepartmentResponse.Projection())
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return department is null ? DirectoryErrors.DepartmentNotFound() : department;
    }
}
