using Itms.Modules.Identity.Authorization;
using Itms.Modules.Identity.Security;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Itms.Modules.Identity.Features.Auth.ChangePassword;

/// <summary><c>POST /api/v1/auth/change-password</c>.</summary>
internal static class ChangePasswordEndpoint
{
    /// <summary>Maps the endpoint onto the auth group.</summary>
    /// <param name="group">The <c>/api/v1/auth</c> group.</param>
    public static void MapChangePassword(this RouteGroupBuilder group)
    {
        group
            .MapPost("/change-password", async (
                ChangePasswordRequest request,
                ChangePasswordHandler handler,
                ICurrentUser currentUser,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                if (currentUser.UserId is not { } userId || !http.User.TryGetSessionId(out var sessionId))
                {
                    return ProblemDetailsMapper.ToProblem(
                        Error.Unauthorized("auth.not_signed_in", "You are not signed in."));
                }

                var result = await handler
                    .HandleAsync(userId, sessionId, request, cancellationToken)
                    .ConfigureAwait(false);

                return result.ToNoContent();
            })
            .RequireAuthorization(ItmsPolicies.Authenticated)
            .AddEndpointFilter<AntiforgeryFilter>()
            .WithValidation<ChangePasswordRequest>()
            .RequireRateLimiting(IdentityRateLimiting.PolicyName)
            .WithName("ChangePassword")
            .WithSummary("Changes the caller's own password and revokes their other sessions.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
