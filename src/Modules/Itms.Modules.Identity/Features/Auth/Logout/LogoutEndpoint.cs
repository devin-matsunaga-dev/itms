using Itms.Modules.Identity.Authorization;
using Itms.Modules.Identity.Security;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace Itms.Modules.Identity.Features.Auth.Logout;

/// <summary><c>POST /api/v1/auth/logout</c>.</summary>
internal static class LogoutEndpoint
{
    /// <summary>Maps the endpoint onto the auth group.</summary>
    /// <param name="group">The <c>/api/v1/auth</c> group.</param>
    public static void MapLogout(this RouteGroupBuilder group)
    {
        group
            .MapPost("/logout", async (
                LogoutHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                if (http.User.TryGetSessionId(out var sessionId))
                {
                    var result = await handler.HandleAsync(sessionId, cancellationToken).ConfigureAwait(false);
                    if (result.IsFailure)
                    {
                        return ProblemDetailsMapper.ToProblem(result.Error);
                    }
                }

                await http.SignOutAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);
                return Microsoft.AspNetCore.Http.Results.NoContent();
            })
            .RequireAuthorization(ItmsPolicies.Authenticated)
            .AddEndpointFilter<AntiforgeryFilter>()
            .WithName("Logout")
            .WithSummary("Revokes the current session and clears the cookie.")
            .Produces(StatusCodes.Status204NoContent);
    }
}
