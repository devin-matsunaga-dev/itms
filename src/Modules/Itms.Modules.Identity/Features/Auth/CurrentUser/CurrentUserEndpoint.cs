using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Itms.Modules.Identity.Features.Auth.CurrentUser;

/// <summary><c>GET /api/v1/auth/me</c>.</summary>
internal static class CurrentUserEndpoint
{
    /// <summary>Maps the endpoint onto the auth group.</summary>
    /// <param name="group">The <c>/api/v1/auth</c> group.</param>
    public static void MapCurrentUser(this RouteGroupBuilder group)
    {
        group
            .MapGet("/me", async (
                CurrentUserHandler handler,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                if (currentUser.UserId is not { } userId)
                {
                    return ProblemDetailsMapper.ToProblem(
                        Error.Unauthorized("auth.not_signed_in", "You are not signed in."));
                }

                var result = await handler.HandleAsync(userId, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .RequireAuthorization(ItmsPolicies.Authenticated)
            .WithName("CurrentUser")
            .WithSummary("Returns the signed-in account, its roles, and its directory placement.")
            .Produces<AuthenticatedUserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
