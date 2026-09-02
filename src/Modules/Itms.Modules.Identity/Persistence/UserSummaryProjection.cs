using System.Linq.Expressions;
using Itms.Contracts.Lookups;
using Itms.Modules.Identity.Domain;

namespace Itms.Modules.Identity.Persistence;

/// <summary>
/// The one shape a user leaves Identity in, written once so no query can widen it.
/// </summary>
/// <remarks>
/// <para>
/// It was private to <c>UserLookupService</c> until WP-2.7, when a second reader appeared:
/// the paged directory list, which cannot go through <c>IUserLookup</c> because
/// <c>Itms.Contracts</c> references nothing in the solution and therefore cannot carry a
/// page envelope. Two readers meant either two projections or one shared one, and two
/// projections of a shape whose whole purpose is that it carries no credential state is
/// exactly the drift worth spending a file to prevent: widening <see cref="UserSummary"/>
/// by editing one query is what this makes impossible.
/// </para>
/// <para>
/// The roles come from a correlated subquery rather than a second round trip — a list of
/// fifty users would otherwise be fifty-one queries. That is also why this is a method
/// taking the context rather than a static field: it has to close over the context to
/// write the subquery, which is the shape <c>LocationQueries.Projection</c> uses for the
/// same reason.
/// </para>
/// </remarks>
internal static class UserSummaryProjection
{
    /// <summary>Projects a user to the summary another module — or the wire — may see.</summary>
    /// <param name="database">The identity context, so the role list is a correlated subquery.</param>
    /// <returns>The projection.</returns>
    public static Expression<Func<ItmsUser, UserSummary>> For(ItmsIdentityDbContext database) =>
        user => new UserSummary(
            user.Id,
            user.DisplayName,
            user.Email!,
            user.DepartmentId,
            user.LocationId,
            user.IsActive,
            database.UserRoles
                .Where(membership => membership.UserId == user.Id)
                .Join(database.Roles, membership => membership.RoleId, role => role.Id, (_, role) => role.Name!)
                // Ordered so the list is stable between reads; nothing depends on which
                // role comes first, and a set that reshuffles is a diff nobody wanted.
                .OrderBy(name => name)
                .ToList());
}
