using Itms.Contracts.Lookups;
using Itms.Modules.Identity.Features.Users.ListUsers;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Itms.Modules.Identity.Features.Users.SearchUsers;

/// <summary>
/// <c>GET /api/v1/users</c> and <c>GET /api/v1/users/{id}</c> — the staff directory, and
/// the requester and assignee picker every module needs.
/// </summary>
/// <remarks>
/// <para>
/// <b>One route serves both, and it is paged.</b> WP-0.5 wrote the list as a picker search
/// — <c>?search=&amp;limit=</c>, active accounts only, capped at fifty, answering a bare
/// array. WP-2.7's directory screen needs filters, an ordering and a page in the URL, and
/// none of those can be honoured against a response that never says how many rows exist, so
/// the route was widened rather than duplicated: a picker is a directory read of one page,
/// and two routes over one table is two things to keep agreeing.
/// </para>
/// <para>
/// Both routes answer <see cref="UserSummary"/> and nothing else. The single read still goes
/// through <see cref="IUserLookup"/>; the list goes through
/// <see cref="ListUsersHandler"/>, which projects with the same expression the lookup does —
/// so the shape another module sees over HTTP is exactly the shape it sees in process, and
/// neither can be widened to include credential state by accident.
/// </para>
/// </remarks>
internal static class UserDirectoryEndpoints
{
    /// <summary>The route prefix these endpoints hang off.</summary>
    public const string RoutePrefix = "/api/v1/users";

    /// <summary>Maps the directory endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    public static void MapUserDirectory(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup(RoutePrefix)
            .WithTags("Users")
            // Technician or Admin. An end user's business is their own tickets
            // (SPEC.md §14); they have no reason to enumerate the staff directory.
            .RequireAuthorization(ItmsPolicies.Technician);

        group
            .MapGet("/", async (
                [AsParameters] ListUsersQuery query,
                ListUsersHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithName("SearchUsers")
            .WithSummary("Lists people, filtered by name, department, location, role, and account status.")
            .WithDescription(
                "The staff directory and the product's people-picker are the same read. A blank "
                + "search term lists rather than refuses, because a picker's first state is the "
                + "list; deactivated accounts are excluded unless includeInactive=true, so nothing "
                + "offers equipment or a ticket to somebody who can no longer sign in.")
            .Produces<PagedResult<UserSummary>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group
            .MapGet("/{id:guid}", async (
                Guid id,
                IUserLookup users,
                CancellationToken cancellationToken) =>
            {
                var user = await users.GetAsync(id, cancellationToken).ConfigureAwait(false);

                return user is null
                    ? ProblemDetailsMapper.ToProblem(Error.NotFound("user.not_found", "No such user."))
                    : Microsoft.AspNetCore.Http.Results.Ok(user);
            })
            .WithName("GetUser")
            .WithSummary("Reads one user's public summary.")
            .Produces<UserSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
