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

    private static System.Linq.Expressions.Expression<Func<Domain.ItmsUser, UserSummary>> Projection() =>
        user => new UserSummary(
            user.Id,
            user.DisplayName,
            user.Email!,
            user.DepartmentId,
            user.LocationId,
            user.IsActive);
}
