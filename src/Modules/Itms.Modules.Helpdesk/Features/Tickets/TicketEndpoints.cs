using Itms.Modules.Helpdesk.Features.TicketHistory;
using Itms.Modules.Helpdesk.Features.TicketHistory.ListTicketHistory;
using Itms.Modules.Helpdesk.Features.Tickets.AssignTicket;
using Itms.Modules.Helpdesk.Features.Tickets.ChangeTicketStatus;
using Itms.Modules.Helpdesk.Features.Tickets.CreateTicket;
using Itms.Modules.Helpdesk.Features.Tickets.GetTicket;
using Itms.Modules.Helpdesk.Features.Tickets.LinkTicketAsset;
using Itms.Modules.Helpdesk.Features.Tickets.ListTickets;
using Itms.Modules.Helpdesk.Features.Tickets.TicketCounters;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Itms.Platform.Security;
using Itms.Platform.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MinimalApi = Microsoft.AspNetCore.Http.Results;

namespace Itms.Modules.Helpdesk.Features.Tickets;

/// <summary>The ticket endpoints, under <c>/api/v1/tickets</c>.</summary>
/// <remarks>
/// <para>
/// WP-1.3 mapped the status change, WP-1.4 the timeline that records it, WP-1.5 the three
/// that make a ticket reachable at all — create, list, detail — WP-1.6 the assignment
/// that decides who works it, and WP-2.5 the asset link that says which machine it is
/// about.
/// </para>
/// <para>
/// <b>The group is no longer Technician-only.</b> ARCHITECTURE.md §7 gives a User their own
/// tickets, and WP-1.5 is the package that had a requester-scoped read to enforce it
/// against. Reads and creation sit on <see cref="ItmsPolicies.Authenticated"/> and are
/// narrowed row by row in the query — see <see cref="TicketScope"/>, which is the
/// enforcement; the policy only says who may ask. <b>Transitions stay on
/// <see cref="ItmsPolicies.Technician"/></b>, unchanged from WP-1.3: a requester cannot
/// cancel or close their own ticket in V1.
/// </para>
/// <para>
/// <b>WP-1.7 must not widen this by accident.</b> Comments will hang off the same routes,
/// and §7 lets a User comment on their own ticket — but an internal note is the one thing
/// they may not read, and no policy here will stop a projection that includes one.
/// </para>
/// </remarks>
internal static class TicketEndpoints
{
    /// <summary>The route prefix these endpoints hang off.</summary>
    public const string RoutePrefix = "/api/v1/tickets";

    /// <summary>Maps the ticket endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    public static void MapTickets(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup(RoutePrefix)
            .WithTags("Tickets")
            .RequireAuthorization(ItmsPolicies.Authenticated);

        MapReads(group);
        MapWrites(group);
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        group
            .MapGet("/counters", async (
                [AsParameters] TicketCountersQuery query,
                TicketCountersHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithName("TicketCounters")
            .WithSummary("Counts the queue: the KPI figures and the saved-view totals.")
            .WithDescription(
                "Scope-wide and deliberately unaffected by any filter — a counter that moved with "
                + "the filters would describe the filter rather than the queue. A User's counts "
                + "cover only the tickets they raised. Send dueBefore as the end of your own day; "
                + "the server falls back to the end of its UTC day.")
            .Produces<TicketCountersResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group
            .MapGet("/", async (
                [AsParameters] ListTicketsQuery query,
                ListTicketsHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithName("ListTickets")
            .WithSummary("Reads the ticket queue, filtered, sorted, and paged.")
            .WithDescription(
                "A Technician or an Admin reads every ticket; anybody else reads only the tickets "
                + "they raised. Defaults to newest first.")
            .Produces<PagedResult<TicketListItemResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group
            .MapGet("/{id:guid}", async (
                Guid id,
                GetTicketHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return WithETag(result, context, detail => detail.Response, detail => detail.Version);
            })
            .WithName("GetTicket")
            .WithSummary("Reads one ticket in full, with the head of its timeline.")
            .WithDescription(
                "Carries an ETag naming the ticket's current version. Send it back as If-Match on a "
                + "status change to be told the ticket has moved before the change is attempted.")
            .Produces<TicketDetailResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group
            .MapGet("/{id:guid}/history", async (
                Guid id,
                ListTicketHistoryHandler handler,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(id, PageRequest.Of(page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToOk();
            })
            .WithName("ListTicketHistory")
            .WithSummary("Reads a ticket's history, newest first.")
            .Produces<PagedResult<TicketHistoryEntryResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapWrites(RouteGroupBuilder group)
    {
        var writes = group
            .MapGroup(string.Empty)
            // Cookie auth plus a state-changing verb is the shape CSRF exploits;
            // CONVENTIONS.md's security floor requires the check on every one of them.
            .AddEndpointFilter<AntiforgeryFilter>();

        writes
            .MapPost("/", async (
                CreateTicketRequest request,
                CreateTicketHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);

                if (result.IsFailure)
                {
                    return ProblemDetailsMapper.ToProblem(result.Error!);
                }

                var created = result.Value;
                SetETag(context, created.Version);

                return MinimalApi.Created($"{RoutePrefix}/{created.Response.Id}", created.Response);
            })
            .WithValidation<CreateTicketRequest>()
            .WithName("CreateTicket")
            .WithSummary("Raises a ticket.")
            .WithDescription(
                "A User may only raise a ticket for themselves and is refused with 403 for naming "
                + "anybody else. The requester defaults to the caller and the department to the "
                + "requester's own.")
            .Produces<TicketDetailResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        writes
            .MapPost("/{id:guid}/status-changes", async (
                Guid id,
                ChangeTicketStatusRequest request,
                ChangeTicketStatusHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(id, request, TicketETag.PreconditionFrom(context.Request), cancellationToken)
                    .ConfigureAwait(false);

                return WithETag(result, context, change => change.Response, change => change.Version);
            })
            // Technician or Admin only, narrower than the group: a requester may read and
            // (from WP-1.7) comment on their own ticket, and nothing else.
            .RequireAuthorization(ItmsPolicies.Technician)
            .WithValidation<ChangeTicketStatusRequest>()
            .WithName("ChangeTicketStatus")
            .WithSummary("Moves a ticket to another status, refusing any transition SPEC.md §2 does not allow.")
            .WithDescription(
                "Send the ticket's ETag as If-Match to be refused with 412 if it has moved since you "
                + "read it. Without the header the request proceeds and a lost race is a 409.")
            .Produces<TicketStatusChangeResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        writes
            .MapPost("/{id:guid}/assignments", async (
                Guid id,
                AssignTicketRequest request,
                AssignTicketHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(id, request, TicketETag.PreconditionFrom(context.Request), cancellationToken)
                    .ConfigureAwait(false);

                return WithETag(result, context, a => a.Response, a => a.Version);
            })
            // Technician or Admin, narrower than the group and matching the transitions: a
            // requester may read and comment on their own ticket, never decide who works it.
            .RequireAuthorization(ItmsPolicies.Technician)
            .WithValidation<AssignTicketRequest>()
            .WithName("AssignTicket")
            .WithSummary("Assigns, reassigns, or unassigns a ticket.")
            .WithDescription(
                "Send an assigneeId to put a technician in charge, or null to unassign. The first "
                + "assignment moves the ticket from New to Assigned and unassigning moves it back; "
                + "reassignment leaves the status alone. Send the ticket's ETag as If-Match to be "
                + "refused with 412 if it has moved since you read it.")
            .Produces<TicketAssignmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        writes
            .MapPost("/{id:guid}/related-asset", async (
                Guid id,
                LinkTicketAssetRequest request,
                LinkTicketAssetHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(id, request, TicketETag.PreconditionFrom(context.Request), cancellationToken)
                    .ConfigureAwait(false);

                return WithETag(result, context, link => link.Response, link => link.Version);
            })
            // Technician or Admin, narrower than the group and matching the other two
            // writes: a requester may read and comment on their own ticket, never decide
            // which piece of equipment it is filed against.
            .RequireAuthorization(ItmsPolicies.Technician)
            .WithValidation<LinkTicketAssetRequest>()
            .WithName("LinkTicketAsset")
            .WithSummary("Names the asset a ticket is about, changes it, or clears the link.")
            .WithDescription(
                "Send an assetId to link the ticket to a piece of equipment, or null to clear the "
                + "link. Linking an asset the ticket already names, or clearing a link it does not "
                + "have, is refused rather than treated as a no-op — either would write a timeline "
                + "line describing a change that did not happen. A closed or cancelled ticket "
                + "cannot be relinked. Send the ticket's ETag as If-Match to be refused with 412 if "
                + "it has moved since you read it.")
            .Produces<TicketAssetLinkResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
    }

    /// <summary>
    /// 200 with the payload and its <c>ETag</c>, or the mapped problem response.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not <c>ToOk</c>, because the header has to be set on the way out and only a success
    /// has a version to set it from. A failure goes through exactly the same mapper every
    /// other endpoint uses.
    /// </para>
    /// <para>
    /// Every read and every write of a ticket answers with a tag, so a client always
    /// leaves an exchange holding a precondition it can state on the next one. WP-1.5 left
    /// the status change without one because the freshness of the <c>xmin</c> shadow
    /// property after <c>SaveChanges</c> had not been checked; WP-1.6 checked it, and the
    /// two writes now answer the same way the detail read does.
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">The handler's result, carrying a response and a version.</typeparam>
    /// <typeparam name="TResponse">The body the client sees.</typeparam>
    /// <param name="result">What the handler returned.</param>
    /// <param name="context">The request, whose response headers the tag is set on.</param>
    /// <param name="response">Reads the body out of the result.</param>
    /// <param name="version">Reads the row version out of the result.</param>
    private static IResult WithETag<TResult, TResponse>(
        Result<TResult> result,
        HttpContext context,
        Func<TResult, TResponse> response,
        Func<TResult, uint> version)
    {
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblem(result.Error!);
        }

        var value = result.Value;
        SetETag(context, version(value));

        return MinimalApi.Ok(response(value));
    }

    private static void SetETag(HttpContext context, uint version) =>
        context.Response.Headers.ETag = TicketETag.For(version);
}
