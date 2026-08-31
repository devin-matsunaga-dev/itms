using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.TicketCategories.ListTicketCategories;

/// <summary>Lists ticket categories in picker order.</summary>
/// <param name="database">The helpdesk context.</param>
internal sealed class ListTicketCategoriesHandler(HelpdeskDbContext database)
{
    /// <summary>Reads a page of categories.</summary>
    /// <param name="includeInactive">True to include retired categories.</param>
    /// <param name="page">The page to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The page envelope.</returns>
    public async Task<Result<PagedResult<TicketCategoryResponse>>> HandleAsync(
        bool includeInactive,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = database.TicketCategories.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(category => category.IsActive);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Sort order first, then name: sort_order is not unique, and a picker whose order
        // changes between two reads of the same data is a bug nobody can reproduce.
        // Projected in the query, never by loading entities and mapping them after
        // (CONVENTIONS.md).
        var items = await query
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.NormalizedName)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(TicketCategoryResponse.Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.From<TicketCategoryResponse>(items, total, page);
    }
}
