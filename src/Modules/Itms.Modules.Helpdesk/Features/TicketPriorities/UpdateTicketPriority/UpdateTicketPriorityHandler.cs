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

namespace Itms.Modules.Helpdesk.Features.TicketPriorities.UpdateTicketPriority;

/// <summary>
/// Edits a priority's name, description, order, and SLA targets. Never its code.
/// </summary>
/// <param name="database">The helpdesk context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
internal sealed class UpdateTicketPriorityHandler(
    HelpdeskDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit)
{
    /// <summary>Applies <paramref name="request"/> to the priority with <paramref name="priorityId"/>.</summary>
    /// <param name="priorityId">The priority to edit.</param>
    /// <param name="request">The new field values.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The edited priority, a not-found, or a conflict on a duplicate name.</returns>
    public async Task<Result<TicketPriorityResponse>> HandleAsync(
        Guid priorityId,
        UpdateTicketPriorityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Error? failure = null;
        TicketPriorityResponse? updated = null;

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

                // Read before the entity mutates: the diff is the whole point of the
                // entry, and after Rename there is nothing left to compare against.
                var previousName = priority.Name;
                var previousDescription = priority.Description;
                var previousRank = priority.Rank;
                var previousResponse = priority.ResponseTargetMinutes;
                var previousResolution = priority.ResolutionTargetMinutes;

                var now = clock.UtcNow;
                var actor = currentUser.UserId;

                priority.Rename(request.Name, now, actor);
                priority.Describe(request.Description, now, actor);
                priority.Reorder(request.Rank, now, actor);
                priority.SetTargets(request.ResponseTargetMinutes, request.ResolutionTargetMinutes, now, actor);

                // The code is not passed: it cannot change, so only the name can newly
                // collide. Run after normalisation, so the check compares the same string
                // the unique index will.
                failure = await TicketPriorityUniqueness
                    .CheckAsync(database, priority.NormalizedName, code: null, priorityId, token)
                    .ConfigureAwait(false);

                if (failure is not null)
                {
                    return;
                }

                await database.SaveChangesAsync(token).ConfigureAwait(false);

                await audit.WriteAsync(
                    new AuditEntry(
                        HelpdeskAudit.PriorityUpdated,
                        HelpdeskAudit.PriorityEntityType,
                        priority.Id.ToString(),
                        HelpdeskAudit.Changes()
                            .Moved("name", previousName, priority.Name)
                            .Moved("description", previousDescription, priority.Description)
                            .Moved("rank", Text(previousRank), Text(priority.Rank))
                            .Moved("responseTargetMinutes", Text(previousResponse), Text(priority.ResponseTargetMinutes))
                            .Moved("resolutionTargetMinutes", Text(previousResolution), Text(priority.ResolutionTargetMinutes))),
                    token).ConfigureAwait(false);

                updated = TicketPriorityResponse.From(priority);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? failure : updated!;
    }

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}
