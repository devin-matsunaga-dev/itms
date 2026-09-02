using Itms.Contracts.Lookups;
using Itms.Modules.Identity.Domain;
using Itms.Modules.Identity.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Identity.Features.Users.ListUsers;

/// <summary>Reads a page of the user directory.</summary>
/// <remarks>
/// <para>
/// <b>Projected, never loaded</b>, through <see cref="UserSummaryProjection"/> — the same
/// expression <c>IUserLookup</c> answers with, so the shape this route puts on the wire is
/// the shape another module sees in process and neither can grow a credential field by
/// somebody editing one query.
/// </para>
/// <para>
/// <b>It queries the context directly rather than going through <see cref="IUserLookup"/>,
/// and that is not a boundary breach.</b> This is Identity reading Identity's own table for
/// Identity's own screen. The contract exists so *other* modules cannot reach in; a paged
/// envelope could not go through it in any case, because <c>Itms.Contracts</c> references
/// nothing in the solution and <c>PagedResult&lt;T&gt;</c> lives in the shared kernel —
/// the same wall that made <c>ITicketLookup</c> declare its own <c>TicketPage</c> at WP-2.5.
/// Adding a paged read to the contract for the sake of one screen would put a page shape in
/// the contracts assembly that no module has asked for.
/// </para>
/// </remarks>
/// <param name="database">The identity context.</param>
internal sealed class ListUsersHandler(ItmsIdentityDbContext database)
{
    /// <summary>Reads a page of people.</summary>
    /// <param name="query">The filters and the ordering.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The page envelope. An empty page is a success, never a 404.</returns>
    public async Task<Result<PagedResult<UserSummary>>> HandleAsync(
        ListUsersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = PageRequest.Of(query.Page, query.PageSize);
        var users = Filter(database, database.Users.AsNoTracking(), query);

        var total = await users.CountAsync(cancellationToken).ConfigureAwait(false);

        if (total == 0)
        {
            // Nothing matched. Worth the branch: a filtered directory is empty often, and
            // the second round trip would return nothing to render.
            return PagedResult.Empty<UserSummary>(page);
        }

        var sort = query.Sort ?? UserSort.DisplayName;

        // A name and an address are asked for because the front of the list is the wanted
        // end — the person being looked for. A creation date means "most recent first".
        var descending = query.Direction switch
        {
            SortDirection.Ascending => false,
            SortDirection.Descending => true,
            _ => sort is UserSort.CreatedAt,
        };

        // Every ordering ends at the id. None of these columns is unique — two people share
        // a creation instant under a fast enough clock, and nothing stops two accounts
        // sharing a display name — and a paged list whose order changes between two reads of
        // the same data silently drops and duplicates rows across page boundaries. WP-1.4
        // learned that from a test rather than from reasoning.
        var ordered = sort switch
        {
            UserSort.Email => descending
                ? users.OrderByDescending(user => user.Email).ThenByDescending(user => user.Id)
                : users.OrderBy(user => user.Email).ThenBy(user => user.Id),

            UserSort.CreatedAt => descending
                ? users.OrderByDescending(user => user.CreatedAt).ThenByDescending(user => user.Id)
                : users.OrderBy(user => user.CreatedAt).ThenBy(user => user.Id),

            _ => descending
                ? users.OrderByDescending(user => user.DisplayName).ThenByDescending(user => user.Id)
                : users.OrderBy(user => user.DisplayName).ThenBy(user => user.Id),
        };

        var items = await ordered
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(UserSummaryProjection.For(database))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.From<UserSummary>(items, total, page);
    }

    /// <summary>Applies the filters that are actually present.</summary>
    /// <remarks>
    /// Each is skipped when absent rather than folded into one expression with null checks
    /// inside it, because a <c>WHERE (@p IS NULL OR col = @p)</c> is the shape that makes
    /// PostgreSQL choose a plan for the first parameter it is given and keep it for every
    /// other combination — the note <c>ListAssetsHandler.Filter</c> carries.
    /// </remarks>
    /// <param name="database">The context, for the role subquery.</param>
    /// <param name="users">The user query to narrow.</param>
    /// <param name="query">The filters asked for.</param>
    /// <returns>The narrowed query.</returns>
    private static IQueryable<ItmsUser> Filter(
        ItmsIdentityDbContext database,
        IQueryable<ItmsUser> users,
        ListUsersQuery query)
    {
        if (query.IncludeInactive is not true)
        {
            users = users.Where(user => user.IsActive);
        }

        if (query.DepartmentId is { } departmentId)
        {
            users = users.Where(user => user.DepartmentId == departmentId);
        }

        if (query.LocationId is { } locationId)
        {
            users = users.Where(user => user.LocationId == locationId);
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            // Matched on the normalised name, which is the column Identity indexes and the
            // one it keeps upper-cased for exactly this kind of comparison. An unrecognised
            // role simply matches no membership row, so the answer is an empty page.
            var normalized = query.Role.Trim().ToUpperInvariant();

            var memberships =
                from membership in database.UserRoles
                join role in database.Roles on membership.RoleId equals role.Id
                where role.NormalizedName == normalized
                select membership.UserId;

            users = users.Where(user => memberships.Contains(user.Id));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // The escaping is the shared kernel's (WP-1.12): an unescaped % or _ typed into
            // the box would otherwise become a wildcard over the whole table.
            var pattern = SearchPattern.Containing(query.Search);

            users = users.Where(user =>
                EF.Functions.ILike(user.DisplayName, pattern, SearchPattern.Escape) ||
                EF.Functions.ILike(user.Email!, pattern, SearchPattern.Escape));
        }

        return users;
    }
}
