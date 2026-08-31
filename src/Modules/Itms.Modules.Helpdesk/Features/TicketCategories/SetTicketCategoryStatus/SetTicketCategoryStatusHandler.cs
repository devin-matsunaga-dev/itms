using Itms.Contracts.Auditing;
using Itms.Modules.Helpdesk.Auditing;
using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.TicketCategories.SetTicketCategoryStatus;

/// <summary>
/// Retires a ticket category or brings it back.
/// </summary>
/// <remarks>
/// This is what stands in for a delete, and there is no delete. WP-1.1's criterion is
/// that a category in use cannot be removed; the strongest form of that is a module with
/// no removal path at all, which is also what keeps every historical ticket's category
/// readable. WP-1.2 adds the ticket foreign key with <c>ON DELETE RESTRICT</c>, so the
/// database refuses as well as the API.
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
internal sealed class SetTicketCategoryStatusHandler(
    HelpdeskDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit)
{
    /// <summary>Sets whether the category is active.</summary>
    /// <param name="categoryId">The category to change.</param>
    /// <param name="isActive">True to reinstate it, false to retire it.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success, or a not-found failure. Setting the state it already has succeeds.</returns>
    public async Task<Result> HandleAsync(Guid categoryId, bool isActive, CancellationToken cancellationToken)
    {
        Error? failure = null;

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

                var wasActive = category.IsActive;
                var now = clock.UtcNow;
                var actor = currentUser.UserId;

                if (isActive)
                {
                    category.Reactivate(now, actor);
                }
                else
                {
                    category.Deactivate(now, actor);
                }

                await database.SaveChangesAsync(token).ConfigureAwait(false);

                // Setting the state it already has is a success, not a change. Auditing it
                // would fill the trail with entries in which nothing moved.
                if (wasActive != category.IsActive)
                {
                    await audit.WriteAsync(
                        new AuditEntry(
                            category.IsActive
                                ? HelpdeskAudit.CategoryReinstated
                                : HelpdeskAudit.CategoryRetired,
                            HelpdeskAudit.CategoryEntityType,
                            category.Id.ToString(),
                            HelpdeskAudit.Changes().Moved(
                                "isActive",
                                wasActive ? "true" : "false",
                                category.IsActive ? "true" : "false")),
                        token).ConfigureAwait(false);
                }
            },
            cancellationToken).ConfigureAwait(false);

        return failure is null ? Result.Success() : Result.Failure(failure);
    }
}
