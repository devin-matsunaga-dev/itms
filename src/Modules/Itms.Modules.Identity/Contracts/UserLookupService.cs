using Itms.Contracts.Lookups;
using Itms.Modules.Identity.Persistence;
using Itms.Platform.Data;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Identity.Contracts;

/// <summary>
/// Identity's half of <see cref="IUserLookup"/> — the only way another module reads a
/// user (ARCHITECTURE.md §3 rule 2).
/// </summary>
/// <remarks>
/// Every query projects straight to <see cref="UserSummary"/>, which carries no
/// credential state at all: there is no code path here that could hand a password hash,
/// a security stamp, or a lockout state to another module even by accident. The
/// projection itself moved to <see cref="UserSummaryProjection"/> at WP-2.7, when the
/// paged directory list became its second reader — it is shared rather than copied
/// precisely so that guarantee is stated in one place.
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
            .Select(UserSummaryProjection.For(database))
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
            .Select(UserSummaryProjection.For(database))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>A blank term lists rather than refuses.</b> It used to return nothing, which made
    /// every picker in the product permanently empty: the client opens one by asking for
    /// <c>?limit=200</c> with no term at all, so an assignee could never be chosen, a
    /// ticket could never leave <c>New</c>, and an administrator could not file on somebody
    /// else's behalf. Nothing caught it because every integration test assigned through the
    /// API with an id it already had, and every component test mocked the fetch — no test
    /// asked this endpoint the question the client actually asks it.
    /// </para>
    /// <para>
    /// Listing is also what the contract says: <c>IUserLookup.SearchAsync</c> is documented
    /// "for requester and assignee pickers", and a picker's first state is the list, not an
    /// empty box waiting to be typed into.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<UserSummary>> SearchAsync(
        string term,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = database.Users.AsNoTracking().Where(user => user.IsActive);

        if (!string.IsNullOrWhiteSpace(term))
        {
            // Escaped, because an unescaped % or _ in a picker's search box would otherwise
            // quietly turn into a wildcard scan of the whole table. The escaping lives in
            // the shared kernel (WP-1.12); this was one of the two copies that put it there.
            var pattern = SearchPattern.Containing(term);

            query = query.Where(user =>
                EF.Functions.ILike(user.DisplayName, pattern, SearchPattern.Escape) ||
                EF.Functions.ILike(user.Email!, pattern, SearchPattern.Escape));
        }

        return await query
            .OrderBy(user => user.DisplayName)
            .Take(Math.Clamp(limit, 1, MaxSearchResults))
            .Select(UserSummaryProjection.For(database))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
