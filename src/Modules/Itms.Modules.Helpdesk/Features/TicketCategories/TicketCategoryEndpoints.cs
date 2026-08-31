using Itms.Modules.Helpdesk.Features.TicketCategories.CreateTicketCategory;
using Itms.Modules.Helpdesk.Features.TicketCategories.GetTicketCategory;
using Itms.Modules.Helpdesk.Features.TicketCategories.ListTicketCategories;
using Itms.Modules.Helpdesk.Features.TicketCategories.SetTicketCategoryStatus;
using Itms.Modules.Helpdesk.Features.TicketCategories.UpdateTicketCategory;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Security;
using Itms.Platform.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Itms.Modules.Helpdesk.Features.TicketCategories;

/// <summary>The ticket-category endpoints, under <c>/api/v1/ticket-categories</c>.</summary>
/// <remarks>
/// Reads are open to any signed-in account, because an end user filing their own ticket
/// has to pick a category. Writes are Admin only: SPEC.md §13 puts "manage ticket
/// categories and priorities" under administration.
/// <para>
/// There is no <c>DELETE</c>. Retirement is the removal path — see
/// <c>SetTicketCategoryStatusHandler</c>.
/// </para>
/// </remarks>
internal static class TicketCategoryEndpoints
{
    /// <summary>The route prefix these endpoints hang off.</summary>
    public const string RoutePrefix = "/api/v1/ticket-categories";

    /// <summary>Maps the ticket-category endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    public static void MapTicketCategories(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix).WithTags("Ticket categories");

        MapReads(group);
        MapWrites(group);
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        var reads = group.MapGroup(string.Empty).RequireAuthorization(ItmsPolicies.Authenticated);

        reads
            .MapGet("/", async (
                ListTicketCategoriesHandler handler,
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
            .WithName("ListTicketCategories")
            .WithSummary("Lists ticket categories in picker order, optionally including retired ones.")
            .Produces<PagedResult<TicketCategoryResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        reads
            .MapGet("/{id:guid}", async (
                Guid id,
                GetTicketCategoryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithName("GetTicketCategory")
            .WithSummary("Reads one ticket category.")
            .Produces<TicketCategoryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapWrites(RouteGroupBuilder group)
    {
        var writes = group
            .MapGroup(string.Empty)
            .RequireAuthorization(ItmsPolicies.Admin)
            // Cookie auth plus a state-changing verb is exactly the shape CSRF exploits;
            // CONVENTIONS.md's security floor requires the check on every one of them.
            .AddEndpointFilter<AntiforgeryFilter>();

        writes
            .MapPost("/", async (
                CreateTicketCategoryRequest request,
                CreateTicketCategoryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
                return result.ToCreated(category => $"{RoutePrefix}/{category.Id}");
            })
            .WithValidation<CreateTicketCategoryRequest>()
            .WithName("CreateTicketCategory")
            .WithSummary("Creates a ticket category.")
            .Produces<TicketCategoryResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        writes
            .MapPut("/{id:guid}", async (
                Guid id,
                UpdateTicketCategoryRequest request,
                UpdateTicketCategoryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, request, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithValidation<UpdateTicketCategoryRequest>()
            .WithName("UpdateTicketCategory")
            .WithSummary("Replaces a ticket category's name, description, and order. Existing tickets follow the rename.")
            .Produces<TicketCategoryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        writes
            .MapPost("/{id:guid}/deactivate", async (
                Guid id,
                SetTicketCategoryStatusHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, isActive: false, cancellationToken).ConfigureAwait(false);
                return result.ToNoContent();
            })
            .WithName("DeactivateTicketCategory")
            .WithSummary("Retires a ticket category. Existing tickets keep resolving it.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        writes
            .MapPost("/{id:guid}/reactivate", async (
                Guid id,
                SetTicketCategoryStatusHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, isActive: true, cancellationToken).ConfigureAwait(false);
                return result.ToNoContent();
            })
            .WithName("ReactivateTicketCategory")
            .WithSummary("Brings a retired ticket category back into use.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
