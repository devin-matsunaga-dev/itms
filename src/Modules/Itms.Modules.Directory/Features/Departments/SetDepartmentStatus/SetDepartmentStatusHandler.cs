using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Features.Departments.SetDepartmentStatus;

/// <summary>
/// Retires a department or brings it back.
/// </summary>
/// <remarks>
/// This is what stands in for a delete. Tickets, users, and assets hold a department id
/// with no foreign key behind it (§3 rule 6), so a deleted row would leave those
/// references dangling with nothing to render; a retired one keeps resolving and simply
/// stops being offered in pickers.
/// </remarks>
/// <param name="database">The directory context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
internal sealed class SetDepartmentStatusHandler(
    DirectoryDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>Sets whether the department is active.</summary>
    /// <param name="departmentId">The department to change.</param>
    /// <param name="isActive">True to reinstate it, false to retire it.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success, or a not-found failure. Setting the state it already has succeeds.</returns>
    public async Task<Result> HandleAsync(Guid departmentId, bool isActive, CancellationToken cancellationToken)
    {
        Error? failure = null;

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

                if (isActive)
                {
                    department.Reactivate(now, actor);
                }
                else
                {
                    department.Deactivate(now, actor);
                }

                await database.SaveChangesAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is null ? Result.Success() : Result.Failure(failure);
    }
}
