using Itms.Modules.Helpdesk.Features.TicketPriorities.CreateTicketPriority;
using Itms.Modules.Helpdesk.Features.TicketPriorities.GetTicketPriority;
using Itms.Modules.Helpdesk.Features.TicketPriorities.ListTicketPriorities;
using Itms.Modules.Helpdesk.Features.TicketPriorities.SetTicketPriorityStatus;
using Itms.Modules.Helpdesk.Features.TicketPriorities.UpdateTicketPriority;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Security;
using Itms.Platform.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Itms.Modules.Helpdesk.Features.TicketPriorities;

/// <summary>The ticket-priority endpoints, under <c>/api/v1/ticket-priorities</c>.</summary>
/// <remarks>
/// Reads are open to any signed-in account, because an end user filing their own ticket
/// has to pick a priority. Writes are Admin only, per SPEC.md §13.
/// <para>
/// There is no <c>DELETE</c>. Retirement is the removal path — see
/// <c>SetTicketPriorityStatusHandler</c>.
/// </para>
/// </remarks>
internal static class TicketPriorityEndpoints
{
    /// <summary>The route prefix these endpoints hang off.</summary>
    public const string RoutePrefix = "/api/v1/ticket-priorities";

    /// <summary>Maps the ticket-priority endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    public static void MapTicketPriorities(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix).WithTags("Ticket priorities");

        MapReads(group);
        MapWrites(group);
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        var reads = group.MapGroup(string.Empty).RequireAuthorization(ItmsPolicies.Authenticated);

        reads
            .MapGet("/", async (
                ListTicketPrioritiesHandler handler,
                bool? includeInactive,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(includeInactive ?? false, PageRequest.Of(page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToOk();
            })
            .WithName("ListTicketPriorities")
            .WithSummary("Lists ticket priorities most urgent first, optionally including retired ones.")
            .Produces<PagedResult<TicketPriorityResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        reads
            .MapGet("/{id:guid}", async (
                Guid id,
                GetTicketPriorityHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithName("GetTicketPriority")
            .WithSummary("Reads one ticket priority.")
            .Produces<TicketPriorityResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapWrites(RouteGroupBuilder group)
    {
        var writes = group
            .MapGroup(string.Empty)
            .RequireAuthorization(ItmsPolicies.Admin)
            .AddEndpointFilter<AntiforgeryFilter>();

        writes
            .MapPost("/", async (
                CreateTicketPriorityRequest request,
                CreateTicketPriorityHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
                return result.ToCreated(priority => $"{RoutePrefix}/{priority.Id}");
            })
            .WithValidation<CreateTicketPriorityRequest>()
            .WithName("CreateTicketPriority")
            .WithSummary("Creates a ticket priority. The code is chosen here and never changes.")
            .Produces<TicketPriorityResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        writes
            .MapPut("/{id:guid}", async (
                Guid id,
                UpdateTicketPriorityRequest request,
                UpdateTicketPriorityHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, request, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithValidation<UpdateTicketPriorityRequest>()
            .WithName("UpdateTicketPriority")
            .WithSummary("Replaces a ticket priority's name, description, order, and SLA targets. The code is immutable.")
            .Produces<TicketPriorityResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        writes
            .MapPost("/{id:guid}/deactivate", async (
                Guid id,
                SetTicketPriorityStatusHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, isActive: false, cancellationToken).ConfigureAwait(false);
                return result.ToNoContent();
            })
            .WithName("DeactivateTicketPriority")
            .WithSummary("Retires a ticket priority. Existing tickets keep resolving it.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        writes
            .MapPost("/{id:guid}/reactivate", async (
                Guid id,
                SetTicketPriorityStatusHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, isActive: true, cancellationToken).ConfigureAwait(false);
                return result.ToNoContent();
            })
            .WithName("ReactivateTicketPriority")
            .WithSummary("Brings a retired ticket priority back into use.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
