using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.TicketPriorities.GetTicketPriority;

/// <summary>Reads one ticket priority.</summary>
/// <param name="database">The helpdesk context.</param>
internal sealed class GetTicketPriorityHandler(HelpdeskDbContext database)
{
    /// <summary>Reads the priority with <paramref name="priorityId"/>.</summary>
    /// <param name="priorityId">The priority to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The priority, or a not-found failure.</returns>
    public async Task<Result<TicketPriorityResponse>> HandleAsync(Guid priorityId, CancellationToken cancellationToken)
    {
        var priority = await database.TicketPriorities
            .AsNoTracking()
            .Where(candidate => candidate.Id == priorityId)
            .Select(TicketPriorityResponse.Projection())
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return priority is null ? HelpdeskErrors.PriorityNotFound() : priority;
    }
}
