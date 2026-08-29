using Itms.Modules.Identity.Security;
using Itms.Platform.Http;
using Itms.Platform.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Itms.Modules.Identity.Features.Auth.Login;

/// <summary><c>POST /api/v1/auth/login</c>.</summary>
internal static class LoginEndpoint
{
    /// <summary>Maps the endpoint onto the auth group.</summary>
    /// <param name="group">The <c>/api/v1/auth</c> group.</param>
    public static void MapLogin(this RouteGroupBuilder group)
    {
        group
            .MapPost("/login", async (
                LoginRequest request,
                LoginHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    request,
                    http.Connection.RemoteIpAddress?.ToString(),
                    http.Request.Headers.UserAgent.ToString(),
                    cancellationToken).ConfigureAwait(false);

                if (result.IsFailure)
                {
                    return ProblemDetailsMapper.ToProblem(result.Error);
                }

                // The cookie is issued here rather than in the handler, so the handler
                // never touches HttpContext and stays testable without a request.
                await http.SignInAsync(
                    IdentityConstants.ApplicationScheme,
                    result.Value.Principal,
                    new AuthenticationProperties { IsPersistent = true }).ConfigureAwait(false);

                return Microsoft.AspNetCore.Http.Results.Ok(result.Value.User);
            })
            .AllowAnonymous()
            // Antiforgery before validation: an unauthenticated cross-site post should be
            // refused before anything looks at what it contains.
            .AddEndpointFilter<AntiforgeryFilter>()
            .WithValidation<LoginRequest>()
            .RequireRateLimiting(IdentityRateLimiting.PolicyName)
            .WithName("Login")
            .WithSummary("Signs in and issues the session cookie.")
            .Produces<AuthenticatedUserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
