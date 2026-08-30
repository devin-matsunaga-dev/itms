using Itms.Modules.Directory.Persistence;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Features.Departments.ListDepartments;

/// <summary>Lists departments, filtered and paged.</summary>
/// <param name="database">The directory context.</param>
internal sealed class ListDepartmentsHandler(DirectoryDbContext database)
{
    /// <summary>Reads a page of departments.</summary>
    /// <param name="search">A free-text fragment matched against name and code, or <see langword="null"/>.</param>
    /// <param name="includeInactive">True to include retired departments.</param>
    /// <param name="page">The page to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The page envelope.</returns>
    public async Task<Result<PagedResult<DepartmentResponse>>> HandleAsync(
        string? search,
        bool includeInactive,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = database.Departments.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(department => department.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern.Containing(search);
            query = query.Where(department =>
                EF.Functions.ILike(department.Name, pattern, LikePattern.Escape) ||
                (department.Code != null && EF.Functions.ILike(department.Code, pattern, LikePattern.Escape)));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Projected in the query, never by loading entities and mapping them after
        // (CONVENTIONS.md).
        var items = await query
            .OrderBy(department => department.NormalizedName)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(DepartmentResponse.Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.From<DepartmentResponse>(items, total, page);
    }
}
