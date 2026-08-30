using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Features.Departments.CreateDepartment;

/// <summary>Creates a department.</summary>
/// <param name="database">The directory context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
internal sealed class CreateDepartmentHandler(
    DirectoryDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>Creates the department described by <paramref name="request"/>.</summary>
    /// <param name="request">The new department's fields.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The created department, or a conflict on a duplicate name or code.</returns>
    public async Task<Result<DepartmentResponse>> HandleAsync(
        CreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var department = Department.Create(
            request.Name,
            request.Code,
            request.Description,
            clock.UtcNow,
            currentUser.UserId);

        Error? failure = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                failure = await DepartmentUniqueness
                    .CheckAsync(database, department.NormalizedName, department.NormalizedCode, excluding: null, token)
                    .ConfigureAwait(false);

                if (failure is not null)
                {
                    return;
                }

                database.Departments.Add(department);
                await database.SaveChangesAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        if (failure is not null)
        {
            return failure;
        }

        return new DepartmentResponse(
            department.Id,
            department.Name,
            department.Code,
            department.Description,
            department.IsActive,
            department.CreatedAt,
            department.UpdatedAt);
    }
}
