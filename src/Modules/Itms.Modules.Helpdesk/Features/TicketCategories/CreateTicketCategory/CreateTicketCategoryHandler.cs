using Itms.Contracts.Auditing;
using Itms.Modules.Helpdesk.Auditing;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;

namespace Itms.Modules.Helpdesk.Features.TicketCategories.CreateTicketCategory;

/// <summary>Creates a ticket category.</summary>
/// <param name="database">The helpdesk context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
internal sealed class CreateTicketCategoryHandler(
    HelpdeskDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit)
{
    /// <summary>Creates the category described by <paramref name="request"/>.</summary>
    /// <param name="request">The new category's fields.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The created category, or a conflict on a duplicate name.</returns>
    public async Task<Result<TicketCategoryResponse>> HandleAsync(
        CreateTicketCategoryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var category = Domain.TicketCategory.Create(
            request.Name,
            request.Description,
            request.SortOrder,
            clock.UtcNow,
            currentUser.UserId);

        Error? failure = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                failure = await TicketCategoryUniqueness
                    .CheckAsync(database, category.NormalizedName, excluding: null, token)
                    .ConfigureAwait(false);

                if (failure is not null)
                {
                    return;
                }

                database.TicketCategories.Add(category);
                await database.SaveChangesAsync(token).ConfigureAwait(false);

                // Inside the transaction, so a create that rolls back leaves no entry
                // claiming it happened.
                await audit.WriteAsync(
                    new AuditEntry(
                        HelpdeskAudit.CategoryCreated,
                        HelpdeskAudit.CategoryEntityType,
                        category.Id.ToString(),
                        HelpdeskAudit.Changes()
                            .Set("name", category.Name)
                            .Set("description", category.Description)
                            .Set("sortOrder", category.SortOrder.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                    token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? failure : TicketCategoryResponse.From(category);
    }
}
