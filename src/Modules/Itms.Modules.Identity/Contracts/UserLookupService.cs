using Itms.Contracts.Lookups;
using Itms.Modules.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Identity.Contracts;

/// <summary>
/// Identity's half of <see cref="IUserLookup"/> — the only way another module reads a
/// user (ARCHITECTURE.md §3 rule 2).
/// </summary>
/// <remarks>
/// Every query projects straight to <see cref="UserSummary"/>, which carries no
/// credential state at all: there is no code path here that could hand a password hash,
/// a security stamp, or a lockout state to another module even by accident.
/// </remarks>
/// <param name="database">The identity context.</param>
internal sealed class UserLookupService(ItmsIdentityDbContext database) : IUserLookup
{
    private const int MaxSearchResults = 50;

    /// <inheritdoc />
    public async Task<UserSummary?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        await database.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(Projection())
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserSummary>> GetManyAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return [];
        }

        // One query for a whole list screen. The alternative — a lookup per row — is how
        // a ticket list becomes fifty round trips.
        var ids = userIds.Distinct().ToArray();

        return await database.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserSummary>> SearchAsync(
        string term,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return [];
        }

        // Escaped, because an unescaped % or _ in a picker's search box would otherwise
        // quietly turn into a wildcard scan of the whole table.
        var pattern = $"%{term.Trim().Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)}%";

        return await database.Users
            .AsNoTracking()
            .Where(user => user.IsActive &&
                (EF.Functions.ILike(user.DisplayName, pattern, "\\") ||
                 EF.Functions.ILike(user.Email!, pattern, "\\")))
            .OrderBy(user => user.DisplayName)
            .Take(Math.Clamp(limit, 1, MaxSearchResults))
            .Select(Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The one shape every read here projects to, so no method can widen what leaves
    /// Identity by editing its own query.
    /// </summary>
    /// <remarks>
    /// The roles come from a correlated subquery rather than a second round trip: a
    /// picker reading fifty users would otherwise be fifty-one queries. It is an instance
    /// method rather than a static one only because it has to close over the context to
    /// write that subquery.
    /// </remarks>
    private System.Linq.Expressions.Expression<Func<Domain.ItmsUser, UserSummary>> Projection() =>
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
