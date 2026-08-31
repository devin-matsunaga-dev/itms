using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.TicketPriorities.ListTicketPriorities;

/// <summary>Lists ticket priorities, most urgent first.</summary>
/// <param name="database">The helpdesk context.</param>
internal sealed class ListTicketPrioritiesHandler(HelpdeskDbContext database)
{
    /// <summary>Reads a page of priorities.</summary>
    /// <param name="includeInactive">True to include retired priorities.</param>
    /// <param name="page">The page to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The page envelope.</returns>
    public async Task<Result<PagedResult<TicketPriorityResponse>>> HandleAsync(
        bool includeInactive,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = database.TicketPriorities.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(priority => priority.IsActive);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Rank first, then name: rank is not unique, and a picker whose order changes
        // between two reads of the same data is a bug nobody can reproduce.
        var items = await query
            .OrderBy(priority => priority.Rank)
            .ThenBy(priority => priority.NormalizedName)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(TicketPriorityResponse.Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.From<TicketPriorityResponse>(items, total, page);
    }
}
