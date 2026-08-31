using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.TicketCategories.GetTicketCategory;

/// <summary>Reads one ticket category.</summary>
/// <param name="database">The helpdesk context.</param>
internal sealed class GetTicketCategoryHandler(HelpdeskDbContext database)
{
    /// <summary>Reads the category with <paramref name="categoryId"/>.</summary>
    /// <param name="categoryId">The category to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The category, or a not-found failure.</returns>
    public async Task<Result<TicketCategoryResponse>> HandleAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await database.TicketCategories
            .AsNoTracking()
            .Where(candidate => candidate.Id == categoryId)
            .Select(TicketCategoryResponse.Projection())
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return category is null ? HelpdeskErrors.CategoryNotFound() : category;
    }
}
