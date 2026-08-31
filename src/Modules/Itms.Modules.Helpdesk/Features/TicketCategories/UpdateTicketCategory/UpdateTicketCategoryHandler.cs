using System.Globalization;
using Itms.Contracts.Auditing;
using Itms.Modules.Helpdesk.Auditing;
using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.TicketCategories.UpdateTicketCategory;

/// <summary>
/// Edits a category's name, description, and order.
/// </summary>
/// <remarks>
/// A rename touches this row and nothing else. Tickets hold the category's id, so every
/// ticket already filed under it reads the new name from its next query — which is the
/// whole reason the name is not copied onto a ticket in the first place.
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
internal sealed class UpdateTicketCategoryHandler(
    HelpdeskDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit)
{
    /// <summary>Applies <paramref name="request"/> to the category with <paramref name="categoryId"/>.</summary>
    /// <param name="categoryId">The category to edit.</param>
    /// <param name="request">The new field values.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The edited category, a not-found, or a conflict on a duplicate name.</returns>
    public async Task<Result<TicketCategoryResponse>> HandleAsync(
        Guid categoryId,
        UpdateTicketCategoryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Error? failure = null;
        TicketCategoryResponse? updated = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                var category = await database.TicketCategories
                    .FirstOrDefaultAsync(candidate => candidate.Id == categoryId, token)
                    .ConfigureAwait(false);

                if (category is null)
                {
                    failure = HelpdeskErrors.CategoryNotFound();
                    return;
                }

                // Read before the entity mutates: the diff is the whole point of the
                // entry, and after Rename there is nothing left to compare against.
                var previousName = category.Name;
                var previousDescription = category.Description;
                var previousSortOrder = category.SortOrder;

                var now = clock.UtcNow;
                var actor = currentUser.UserId;

                category.Rename(request.Name, now, actor);
                category.Describe(request.Description, now, actor);
                category.Reorder(request.SortOrder, now, actor);

                // Run after the entity has normalised the input, so the check compares the
                // same string the unique index will.
                failure = await TicketCategoryUniqueness
                    .CheckAsync(database, category.NormalizedName, categoryId, token)
                    .ConfigureAwait(false);

                if (failure is not null)
                {
                    return;
                }

                await database.SaveChangesAsync(token).ConfigureAwait(false);

                await audit.WriteAsync(
                    new AuditEntry(
                        HelpdeskAudit.CategoryUpdated,
                        HelpdeskAudit.CategoryEntityType,
                        category.Id.ToString(),
                        HelpdeskAudit.Changes()
                            .Moved("name", previousName, category.Name)
                            .Moved("description", previousDescription, category.Description)
                            .Moved(
                                "sortOrder",
                                previousSortOrder.ToString(CultureInfo.InvariantCulture),
                                category.SortOrder.ToString(CultureInfo.InvariantCulture))),
                    token).ConfigureAwait(false);

                updated = TicketCategoryResponse.From(category);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? failure : updated!;
    }
}
