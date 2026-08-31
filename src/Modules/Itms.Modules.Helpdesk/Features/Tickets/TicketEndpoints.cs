using Itms.Modules.Helpdesk.Features.Tickets.ChangeTicketStatus;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Security;
using Itms.Platform.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Itms.Modules.Helpdesk.Features.Tickets;

/// <summary>The ticket endpoints, under <c>/api/v1/tickets</c>.</summary>
/// <remarks>
/// <para>
/// WP-1.3 maps one route: the status change. Creation, the list, and the detail are
/// WP-1.5's and join this group.
/// </para>
/// <para>
/// <b>Every transition is Technician or Admin.</b> ARCHITECTURE.md §7's row-level rule is
/// that a User may read and comment on their own tickets "and nothing else", so a
/// requester cannot cancel or close their own ticket in V1 — confirmed at the human's
/// direction. A User calling this gets 403, which is enforced here and not merely absent
/// from the interface.
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
            .RequireAuthorization(ItmsPolicies.Technician)
            // Cookie auth plus a state-changing verb is the shape CSRF exploits;
            // CONVENTIONS.md's security floor requires the check on every one of them.
            .AddEndpointFilter<AntiforgeryFilter>();

        group
            .MapPost("/{id:guid}/status-changes", async (
                Guid id,
                ChangeTicketStatusRequest request,
                ChangeTicketStatusHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, request, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithValidation<ChangeTicketStatusRequest>()
            .WithName("ChangeTicketStatus")
            .WithSummary("Moves a ticket to another status, refusing any transition SPEC.md §2 does not allow.")
            .Produces<TicketStatusChangeResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
