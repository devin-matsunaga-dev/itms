using Itms.Modules.Identity.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Identity.Features.Auth.CurrentUser;

/// <summary>
/// Answers "who am I" from the database rather than from the cookie, so a display name
/// or a department changed since sign-in is current on the next page load.
/// </summary>
/// <param name="database">The identity context.</param>
internal sealed class CurrentUserHandler(ItmsIdentityDbContext database)
{
    /// <summary>Reads the signed-in account.</summary>
    /// <param name="userId">The caller, from their principal.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The account, or a failure if the row has gone since the cookie was issued.</returns>
    public async Task<Result<AuthenticatedUserResponse>> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Projected in the query, never materialised as an aggregate (CONVENTIONS.md).
        // The roles subquery is what keeps this one round trip.
        var user = await database.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new AuthenticatedUserResponse(
                candidate.Id,
                candidate.UserName!,
                candidate.Email!,
                candidate.DisplayName,
                database.UserRoles
                    .Where(membership => membership.UserId == candidate.Id)
                    .Join(database.Roles, membership => membership.RoleId, role => role.Id, (_, role) => role.Name!)
                    .ToList(),
                candidate.DepartmentId,
                candidate.LocationId))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return user is null
            ? Error.Unauthorized("auth.session_stale", "The signed-in account no longer exists.")
            : user;
    }
}
