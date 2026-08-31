using System.Globalization;
using Itms.Contracts.Auditing;
using Itms.Modules.Helpdesk.Auditing;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;

namespace Itms.Modules.Helpdesk.Features.TicketPriorities.CreateTicketPriority;

/// <summary>Creates a ticket priority.</summary>
/// <param name="database">The helpdesk context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
internal sealed class CreateTicketPriorityHandler(
    HelpdeskDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit)
{
    /// <summary>Creates the priority described by <paramref name="request"/>.</summary>
    /// <param name="request">The new priority's fields.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The created priority, or a conflict on a duplicate name or code.</returns>
    public async Task<Result<TicketPriorityResponse>> HandleAsync(
        CreateTicketPriorityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var priority = Domain.TicketPriority.Create(
            request.Code,
            request.Name,
            request.Description,
            request.Rank,
            request.ResponseTargetMinutes,
            request.ResolutionTargetMinutes,
            clock.UtcNow,
            currentUser.UserId);

        Error? failure = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                failure = await TicketPriorityUniqueness
                    .CheckAsync(database, priority.NormalizedName, priority.Code, excluding: null, token)
                    .ConfigureAwait(false);

                if (failure is not null)
                {
                    return;
                }

                database.TicketPriorities.Add(priority);
                await database.SaveChangesAsync(token).ConfigureAwait(false);

                // Inside the transaction, so a create that rolls back leaves no entry
                // claiming it happened.
                await audit.WriteAsync(
                    new AuditEntry(
                        HelpdeskAudit.PriorityCreated,
                        HelpdeskAudit.PriorityEntityType,
                        priority.Id.ToString(),
                        HelpdeskAudit.Changes()
                            .Set("code", priority.Code)
                            .Set("name", priority.Name)
                            .Set("description", priority.Description)
                            .Set("rank", priority.Rank.ToString(CultureInfo.InvariantCulture))
                            .Set("responseTargetMinutes", priority.ResponseTargetMinutes.ToString(CultureInfo.InvariantCulture))
                            .Set("resolutionTargetMinutes", priority.ResolutionTargetMinutes.ToString(CultureInfo.InvariantCulture))),
                    token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? failure : TicketPriorityResponse.From(priority);
    }
}
