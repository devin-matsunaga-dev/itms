using Itms.Contracts.Events;
using Itms.Contracts.Lookups;
using Itms.Contracts.Messaging;
using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.Tickets.CreateTicket;

/// <summary>
/// Raises a ticket: the entry point of the core operating loop.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the first cross-module read in the system.</b> ARCHITECTURE.md §3 rule 2
/// says a module reads another's data through the owning module's public contract, and
/// until now nothing had needed to — the rule was enforced only negatively, by forbidding
/// the project reference. Here Helpdesk genuinely needs a person's name and a department's
/// name, and it takes both through <see cref="IUserLookup"/> and
/// <see cref="IDepartmentLookup"/> without knowing that Identity or Directory exist.
/// </para>
/// <para>
/// <b>Why the names are copied onto the row.</b> §3 rule 6: an id plus a cached display
/// string, never a foreign key across a boundary. The cost is staleness — nothing refreshes
/// these until Identity and Directory publish rename events — and the alternative, resolving
/// a name per row, is what WP-1.5's own fifty-thousand-ticket criterion rules out.
/// </para>
/// <para>
/// <b>Everything that must not half-happen is in one transaction:</b> the number claim, the
/// row, and the <see cref="TicketCreated"/> outbox entry. WP-1.2 made the claim refuse to
/// run outside one, precisely so a creation that fails cannot burn a number and leave a gap.
/// </para>
/// <para>
/// <b>No audit call here, deliberately.</b> The Audit module has consumed
/// <see cref="TicketCreated"/> since WP-0.7 and writes its own row from the event. Writing
/// one through <c>IAuditWriter</c> as well would record every creation twice — which is the
/// trap WP-1.3 left behind for status changes and this package is careful not to repeat.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="numbers">Issues the human-readable ticket number.</param>
/// <param name="clock">The system clock. Every instant this writes comes from here.</param>
/// <param name="currentUser">Who is making the request.</param>
/// <param name="users">Identity's public contract, for the requester's name.</param>
/// <param name="departments">Directory's public contract, for the department's name.</param>
/// <param name="publisher">The outbox, enrolled in this handler's own transaction.</param>
internal sealed class CreateTicketHandler(
    HelpdeskDbContext database,
    IModuleDbSession session,
    TicketNumberGenerator numbers,
    IClock clock,
    ICurrentUser currentUser,
    IUserLookup users,
    IDepartmentLookup departments,
    IEventPublisher publisher)
{
    /// <summary>Raises the ticket described by <paramref name="request"/>.</summary>
    /// <param name="request">The ticket to raise.</param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>The new ticket as the detail endpoint renders it, or the failure that stopped it.</returns>
    public async Task<Result<TicketDetail>> HandleAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requesterResolution = ResolveRequesterId(request);

        if (requesterResolution.IsFailure)
        {
            return Result.Failure<TicketDetail>(requesterResolution.Error!);
        }

        var requesterId = requesterResolution.Value;

        // Read outside the transaction: these are reads of other modules' data through
        // their contracts, and holding the ticket-number counter's row lock across them
        // would serialise every creation in the system behind two lookups.
        var requester = await users.GetAsync(requesterId, cancellationToken).ConfigureAwait(false);

        if (requester is null)
        {
            return Result.Failure<TicketDetail>(HelpdeskErrors.RequesterNotFound());
        }

        if (!requester.IsActive)
        {
            return Result.Failure<TicketDetail>(HelpdeskErrors.RequesterInactive());
        }

        var departmentId = request.DepartmentId ?? requester.DepartmentId;

        if (departmentId is null)
        {
            return Result.Failure<TicketDetail>(HelpdeskErrors.DepartmentRequired());
        }

        var department = await departments.GetAsync(departmentId.Value, cancellationToken).ConfigureAwait(false);

        if (department is null)
        {
            return Result.Failure<TicketDetail>(HelpdeskErrors.DepartmentNotFound());
        }

        if (!department.IsActive)
        {
            return Result.Failure<TicketDetail>(HelpdeskErrors.DepartmentRetired());
        }

        var reference = await ReadReferenceDataAsync(request, cancellationToken).ConfigureAwait(false);

        if (reference.IsFailure)
        {
            return Result.Failure<TicketDetail>(reference.Error!);
        }

        var priorityName = reference.Value.PriorityName;

        TicketDetail? created = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                // Claimed inside the transaction and held to commit. That is the price of
                // "no gaps" WP-1.2 chose deliberately over a PostgreSQL sequence.
                var number = await numbers.ClaimAsync(token).ConfigureAwait(false);

                var ticket = Ticket.Create(
                    number,
                    new NewTicket(
                        request.Subject,
                        request.Description,
                        requester.Id,
                        requester.DisplayName,
                        department.Id,
                        department.Name,
                        request.CategoryId,
                        request.PriorityId),
                    clock.UtcNow,
                    currentUser.UserId);

                database.Tickets.Add(ticket);

                await database.SaveChangesAsync(token).ConfigureAwait(false);

                // Staged into the same transaction, so a rollback below this line leaves no
                // event claiming a ticket exists. The priority travels as its name because
                // the event is a fact a consumer renders, not an id it looks up.
                await publisher.PublishAsync(
                    new TicketCreated(
                        ticket.Id,
                        ticket.Number,
                        ticket.RequesterId,
                        ticket.CategoryId,
                        priorityName,
                        ticket.Subject),
                    token).ConfigureAwait(false);

                // Read back through the same projection the detail endpoint uses, rather
                // than assembled by hand here. One shape, built in one place: a create
                // response that drifted from the detail response would be a bug nobody
                // finds until a screen renders one of them wrong.
                created = await TicketDetailResponse
                    .Project(database.Tickets.AsNoTracking().Where(t => t.Id == ticket.Id), database)
                    .SingleAsync(token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        var detail = created!;

        return Result.Success(detail with
        {
            Response = detail.Response with
            {
                // A ticket nothing has happened to yet: an empty timeline, and the moves it
                // can make read off the state machine like everywhere else.
                AllowedNextStatuses = TicketStateMachine.DestinationsFrom(detail.Response.Status),
                History = [],
                HasMoreHistory = false,
            },
        });
    }

    /// <summary>
    /// Works out who the ticket is for, and refuses a User naming anybody but themselves.
    /// </summary>
    /// <remarks>
    /// The refusal is deliberate and was chosen at the human's direction over silently
    /// substituting the caller's own id: a client that sends the wrong requester has a bug,
    /// and one that sends somebody else's on purpose is trying something — coercing either
    /// into a success makes both invisible.
    /// </remarks>
    private Result<Guid> ResolveRequesterId(CreateTicketRequest request)
    {
        if (TicketScope.SeesEveryTicket(currentUser))
        {
            // A Technician or an Admin may file on anybody's behalf — that is most of what
            // a service desk does — but still defaults to themselves when they say nothing.
            var requesterId = request.RequesterId ?? currentUser.UserId;

            return requesterId is null
                ? Result.Failure<Guid>(HelpdeskErrors.RequesterNotFound())
                : Result.Success(requesterId.Value);
        }

        if (currentUser.UserId is not { } self)
        {
            return Result.Failure<Guid>(HelpdeskErrors.RequesterNotFound());
        }

        if (request.RequesterId is { } named && named != self)
        {
            return Result.Failure<Guid>(HelpdeskErrors.RequesterNotSelf());
        }

        return Result.Success(self);
    }

    /// <summary>
    /// Checks the category and the priority exist and are still offered, and reads the
    /// priority's name for the event.
    /// </summary>
    /// <remarks>
    /// Both are this module's own rows, so this is a plain query rather than a contract
    /// call. Retired reference data is refused for a <em>new</em> ticket only — WP-1.1
    /// retires rather than deletes precisely so tickets already filed against it keep
    /// resolving.
    /// </remarks>
    private async Task<Result<TicketReferenceData>> ReadReferenceDataAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var category = await database.TicketCategories
            .AsNoTracking()
            .Where(c => c.Id == request.CategoryId)
            .Select(c => new { c.IsActive })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (category is null)
        {
            return Result.Failure<TicketReferenceData>(HelpdeskErrors.CategoryNotFound());
        }

        if (!category.IsActive)
        {
            return Result.Failure<TicketReferenceData>(HelpdeskErrors.CategoryRetired());
        }

        var priority = await database.TicketPriorities
            .AsNoTracking()
            .Where(p => p.Id == request.PriorityId)
            .Select(p => new { p.Name, p.IsActive })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (priority is null)
        {
            return Result.Failure<TicketReferenceData>(HelpdeskErrors.PriorityNotFound());
        }

        return priority.IsActive
            ? Result.Success(new TicketReferenceData(priority.Name))
            : Result.Failure<TicketReferenceData>(HelpdeskErrors.PriorityRetired());
    }

    /// <summary>What the reference-data check found that creation still needs afterwards.</summary>
    /// <param name="PriorityName">The priority's name, carried on <see cref="TicketCreated"/>.</param>
    private sealed record TicketReferenceData(string PriorityName);
}
