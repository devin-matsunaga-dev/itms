using Itms.Contracts.Lookups;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Itms.Modules.Identity.Features.Users.SearchUsers;

/// <summary>
/// <c>GET /api/v1/users</c> and <c>GET /api/v1/users/{id}</c> — the requester and
/// assignee picker every later module needs.
/// </summary>
/// <remarks>
/// Both read through <see cref="IUserLookup"/> rather than the context, so the shape
/// another module sees over HTTP is exactly the shape it sees in process, and neither
/// can be widened to include credential state by accident.
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
                IUserLookup users,
                string? search,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var matches = await users
                    .SearchAsync(search ?? string.Empty, limit ?? 20, cancellationToken)
                    .ConfigureAwait(false);

                return Microsoft.AspNetCore.Http.Results.Ok(matches);
            })
            .WithName("SearchUsers")
            .WithSummary("Finds active users by name or email, for a picker.")
            .Produces<IReadOnlyList<UserSummary>>()
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
