using Itms.Modules.Helpdesk.Features.TicketComments.AddTicketComment;
using Itms.Modules.Helpdesk.Features.TicketComments.ListTicketComments;
using Itms.Modules.Helpdesk.Features.Tickets;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Security;
using Itms.Platform.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MinimalApi = Microsoft.AspNetCore.Http.Results;

namespace Itms.Modules.Helpdesk.Features.TicketComments;

/// <summary>The comment endpoints, under <c>/api/v1/tickets/{ticketId}/comments</c>.</summary>
/// <remarks>
/// <para>
/// <b>Both sit on <see cref="ItmsPolicies.Authenticated"/>, not on Technician</b>, because
/// ARCHITECTURE.md §7 and SPEC.md §14 both give a User the right to comment on their own
/// ticket. The policy says who may ask; <see cref="TicketScope"/> says which tickets they
/// may ask about and <see cref="TicketVisibility"/> which lines come back. That is the
/// division WP-1.5 established for the ticket reads, applied one level down.
/// </para>
/// <para>
/// <b>There is no edit and no delete route.</b> WP-1.7 was not asked for one and did not
/// invent one — a thread that can be rewritten is not a record of what was said, and on a
/// package the human reviews line by line an unasked-for affordance is the wrong kind of
/// surprise. A package that wants one has to argue for it.
/// </para>
/// </remarks>
internal static class TicketCommentEndpoints
{
    /// <summary>The route these endpoints hang off, under the ticket they belong to.</summary>
    public const string RoutePrefix = TicketEndpoints.RoutePrefix + "/{ticketId:guid}/comments";

    /// <summary>Maps the comment endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    public static void MapTicketComments(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(RoutePrefix)
            .WithTags("Tickets")
            .RequireAuthorization(ItmsPolicies.Authenticated);

        group
            .MapGet("/", async (
                Guid ticketId,
                ListTicketCommentsHandler handler,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(ticketId, PageRequest.Of(page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToOk();
            })
            .WithName("ListTicketComments")
            .WithSummary("Reads a ticket's comments, newest first.")
            .WithDescription(
                "A Technician or an Admin sees internal notes alongside public comments; the requester "
                + "sees only the public ones, and the total counts only what they can see.")
            .Produces<PagedResult<TicketCommentResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group
            .MapPost("/", async (
                Guid ticketId,
                AddTicketCommentRequest request,
                AddTicketCommentHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(ticketId, request, cancellationToken).ConfigureAwait(false);

                return result.IsFailure
                    ? ProblemDetailsMapper.ToProblem(result.Error!)
                    : MinimalApi.Created(
                        $"{TicketEndpoints.RoutePrefix}/{ticketId}/comments/{result.Value.Id}",
                        result.Value);
            })
            // Cookie auth plus a state-changing verb is the shape CSRF exploits.
            .AddEndpointFilter<AntiforgeryFilter>()
            .WithValidation<AddTicketCommentRequest>()
            .WithName("AddTicketComment")
            .WithSummary("Posts a comment, or an internal note, on a ticket.")
            .WithDescription(
                "Set isInternal to keep the comment inside the queue. Only a Technician or an Admin "
                + "may do that; a requester attempting it is refused with 403 rather than having their "
                + "note quietly published. Any status accepts a comment, including a closed ticket.")
            .Produces<TicketCommentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
