using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Features.Departments.UpdateDepartment;

/// <summary>Edits a department's name, code, and description.</summary>
/// <param name="database">The directory context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
internal sealed class UpdateDepartmentHandler(
    DirectoryDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>Applies <paramref name="request"/> to the department with <paramref name="departmentId"/>.</summary>
    /// <param name="departmentId">The department to edit.</param>
    /// <param name="request">The new field values.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The edited department, a not-found, or a conflict on a duplicate name or code.</returns>
    public async Task<Result<DepartmentResponse>> HandleAsync(
        Guid departmentId,
        UpdateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Error? failure = null;
        DepartmentResponse? updated = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                var department = await database.Departments
                    .FirstOrDefaultAsync(candidate => candidate.Id == departmentId, token)
                    .ConfigureAwait(false);

                if (department is null)
                {
                    failure = DirectoryErrors.DepartmentNotFound();
                    return;
                }

                var now = clock.UtcNow;
                var actor = currentUser.UserId;

                department.Rename(request.Name, now, actor);
                department.SetCode(request.Code, now, actor);
                department.Describe(request.Description, now, actor);

                // Run after the entity has normalised the input, so the check compares the
                // same strings the unique indexes will.
                failure = await DepartmentUniqueness
                    .CheckAsync(database, department.NormalizedName, department.NormalizedCode, departmentId, token)
                    .ConfigureAwait(false);

                if (failure is not null)
                {
                    return;
                }

                await database.SaveChangesAsync(token).ConfigureAwait(false);

                updated = new DepartmentResponse(
                    department.Id,
                    department.Name,
                    department.Code,
                    department.Description,
                    department.IsActive,
                    department.CreatedAt,
                    department.UpdatedAt);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? failure : updated!;
    }
}
