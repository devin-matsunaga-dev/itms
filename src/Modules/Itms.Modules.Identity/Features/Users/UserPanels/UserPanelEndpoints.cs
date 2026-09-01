using Itms.Contracts.Lookups;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MinimalApi = Microsoft.AspNetCore.Http.Results;

namespace Itms.Modules.Identity.Features.Users.UserPanels;

/// <summary>
/// The panels of the user 360 page: what somebody is holding, and what they have asked
/// for. Under <c>/api/v1/users/{id}</c>, beside the profile read.
/// </summary>
/// <remarks>
/// <para>
/// <b>One round trip per panel</b>, which is WP-2.5's own acceptance criterion. The profile
/// panel is the existing <c>GET /api/v1/users/{id}</c>; the equipment and the support
/// history are these two. They are deliberately not folded into one aggregate response: a
/// screen that opens on the profile and lazily fills the rest, or one that refreshes only
/// the tickets after a change, would otherwise have to re-read everything.
/// </para>
/// <para>
/// <b>Identity aggregates, and references nobody.</b> Both panels are read through
/// <see cref="IAssetLookup"/> and <see cref="ITicketLookup"/>, so this module still
/// declares references to <c>Itms.Platform</c> and <c>Itms.Contracts</c> and to no module
/// assembly at all — the boundary tests fail the build the day that stops being true. The
/// routes live here because Identity owns <c>/api/v1/users</c>, and a person is the thing
/// being asked about.
/// </para>
/// <para>
/// <b>Authorization is a self-or-technician check, not the group's policy.</b> The rest of
/// <c>/api/v1/users</c> is Technician-or-Admin because an end user has no business
/// enumerating the staff directory. These two are different: SPEC.md §4's user page is
/// also the "what am I holding" self-service view, and refusing it to the person it is
/// about would make the product's own asset endpoints the only route to a fact about
/// themselves. A Technician or an Admin reads anybody; anybody else reads only their own
/// id and is refused with 403 for naming somebody else's.
/// </para>
/// <para>
/// <b>A 403 here rather than the 404 <c>TicketScope</c> gives.</b> The two are different
/// questions: the ticket scope hides <em>which tickets exist</em>, where telling a 403 from
/// a 404 would let an account walk the id space and count what it cannot see. A user id is
/// not a secret — the caller either knows their own or is asking about somebody else on
/// purpose — so the honest refusal is the useful one.
/// </para>
/// </remarks>
internal static class UserPanelEndpoints
{
    /// <summary>The route prefix these endpoints hang off.</summary>
    public const string RoutePrefix = "/api/v1/users/{id:guid}";

    /// <summary>Maps the user-page panels.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    public static void MapUserPanels(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup(RoutePrefix)
            .WithTags("Users")
            // Any authenticated account may ask; who they may ask about is decided per
            // request below. The policy says who may knock, never what they may see.
            .RequireAuthorization(ItmsPolicies.Authenticated);

        group
            .MapGet("/assets", async (
                Guid id,
                IAssetLookup assets,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                if (Refuse(id, currentUser) is { } refusal)
                {
                    return refusal;
                }

                var held = await assets.GetForUserAsync(id, cancellationToken).ConfigureAwait(false);

                return MinimalApi.Ok(held);
            })
            .WithName("ListUserAssets")
            .WithSummary("Reads the equipment currently issued to somebody.")
            .WithDescription(
                "Everything assigned to the user right now, by asset tag. Unpaged: this answers "
                + "what one person is holding, which is a handful of things rather than a queue. "
                + "A Technician or an Admin may ask about anybody; anybody else only about "
                + "themselves.")
            .Produces<IReadOnlyList<AssetSummary>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group
            .MapGet("/tickets", async (
                Guid id,
                TicketActivity? state,
                int? page,
                int? pageSize,
                ITicketLookup tickets,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                if (Refuse(id, currentUser) is { } refusal)
                {
                    return refusal;
                }

                var request = PageRequest.Of(page, pageSize);

                var result = await tickets
                    .GetForRequesterAsync(
                        id,
                        state ?? TicketActivity.All,
                        request.Page,
                        request.PageSize,
                        cancellationToken)
                    .ConfigureAwait(false);

                return MinimalApi.Ok(
                    new PagedResult<TicketSummary>(result.Items, result.Total, result.Page, result.PageSize));
            })
            .WithName("ListUserTickets")
            .WithSummary("Reads the tickets somebody raised, newest first.")
            .WithDescription(
                "state=Open is what is still being worked and state=Past is what is finished with "
                + "— resolved, closed, or cancelled. The two are complementary, so the pair is the "
                + "whole history and nothing appears in both; omitting state returns every ticket. "
                + "Rows carry the ticket summary rather than the full ticket: follow one to "
                + "GET /api/v1/tickets/{id} for the detail.")
            .Produces<PagedResult<TicketSummary>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// The refusal to send when <paramref name="currentUser"/> may not ask about
    /// <paramref name="subjectId"/>, or <see langword="null"/> when they may.
    /// </summary>
    /// <remarks>
    /// Written once and called by both panels, because the two routes have identical rules
    /// and a rule stated twice is a rule that will eventually be stated differently. It
    /// deliberately does not check that the user exists: an id nobody has answers an empty
    /// panel, and the profile read beside it is the route that says 404.
    /// </remarks>
    /// <param name="subjectId">The person being asked about.</param>
    /// <param name="currentUser">Who is asking.</param>
    /// <returns>A 403 problem response, or <see langword="null"/> to proceed.</returns>
    private static IResult? Refuse(Guid subjectId, ICurrentUser currentUser)
    {
        var mayReadAnybody = currentUser.IsInRole(ItmsRoles.Technician) || currentUser.IsInRole(ItmsRoles.Admin);

        return mayReadAnybody || currentUser.UserId == subjectId
            ? null
            : ProblemDetailsMapper.ToProblem(
                Error.Forbidden("identity.user_not_self", "You can only read your own assets and tickets."));
    }
}
