using Itms.Contracts.Auditing;
using Itms.Modules.Helpdesk.Auditing;
using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.TicketPriorities.SetTicketPriorityStatus;

/// <summary>
/// Retires a ticket priority or brings it back.
/// </summary>
/// <remarks>
/// This is what stands in for a delete, and there is no delete — see
/// <c>SetTicketCategoryStatusHandler</c> for the reasoning, which is identical.
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
internal sealed class SetTicketPriorityStatusHandler(
    HelpdeskDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit)
{
    /// <summary>Sets whether the priority is active.</summary>
    /// <param name="priorityId">The priority to change.</param>
    /// <param name="isActive">True to reinstate it, false to retire it.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success, or a not-found failure. Setting the state it already has succeeds.</returns>
    public async Task<Result> HandleAsync(Guid priorityId, bool isActive, CancellationToken cancellationToken)
    {
        Error? failure = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                var priority = await database.TicketPriorities
                    .FirstOrDefaultAsync(candidate => candidate.Id == priorityId, token)
                    .ConfigureAwait(false);

                if (priority is null)
                {
                    failure = HelpdeskErrors.PriorityNotFound();
                    return;
                }

                var wasActive = priority.IsActive;
                var now = clock.UtcNow;
                var actor = currentUser.UserId;

                if (isActive)
                {
                    priority.Reactivate(now, actor);
                }
                else
                {
                    priority.Deactivate(now, actor);
                }

                await database.SaveChangesAsync(token).ConfigureAwait(false);

                // Setting the state it already has is a success, not a change.
                if (wasActive != priority.IsActive)
                {
                    await audit.WriteAsync(
                        new AuditEntry(
                            priority.IsActive
                                ? HelpdeskAudit.PriorityReinstated
                                : HelpdeskAudit.PriorityRetired,
                            HelpdeskAudit.PriorityEntityType,
                            priority.Id.ToString(),
                            HelpdeskAudit.Changes().Moved(
                                "isActive",
                                wasActive ? "true" : "false",
                                priority.IsActive ? "true" : "false")),
                        token).ConfigureAwait(false);
                }
            },
            cancellationToken).ConfigureAwait(false);

        return failure is null ? Result.Success() : Result.Failure(failure);
    }
}
