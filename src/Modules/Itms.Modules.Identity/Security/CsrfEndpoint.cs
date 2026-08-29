using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Itms.Modules.Identity.Security;

/// <summary>The token the browser needs before it may post to a credential endpoint.</summary>
/// <param name="Token">The request token, sent back in <paramref name="HeaderName"/>.</param>
/// <param name="HeaderName">The header the token belongs in, so the client never hard-codes it.</param>
public sealed record CsrfTokenResponse(string Token, string HeaderName);

/// <summary><c>GET /api/v1/auth/csrf</c>.</summary>
internal static class CsrfEndpoint
{
    /// <summary>Maps the endpoint onto the auth group.</summary>
    /// <param name="group">The <c>/api/v1/auth</c> group.</param>
    public static void MapCsrfToken(this RouteGroupBuilder group)
    {
        group
            .MapGet("/csrf", (HttpContext http, IAntiforgery antiforgery) =>
            {
                // Storing the tokens sets the companion cookie; the returned value is the
                // half the client has to echo back, which is what a cross-site page cannot read.
                var tokens = antiforgery.GetAndStoreTokens(http);
                return Microsoft.AspNetCore.Http.Results.Ok(
                    new CsrfTokenResponse(tokens.RequestToken!, tokens.HeaderName!));
            })
            .AllowAnonymous()
            .WithName("CsrfToken")
            .WithSummary("Issues an antiforgery token for the credential endpoints.")
            .Produces<CsrfTokenResponse>();
    }
}
