using System.Security.Claims;
using Itms.Modules.Identity.Persistence;
using Itms.Platform.Time;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itms.Modules.Identity.Authorization;

/// <summary>
/// What the cookie handler does on each request, and what it does instead of
/// redirecting to a login page.
/// </summary>
internal static class SessionCookieEvents
{
    /// <summary>
    /// Checks, on every request, that the session named in the cookie is still live and
    /// its owner still active.
    /// </summary>
    /// <param name="context">The handler's validation context.</param>
    /// <remarks>
    /// This runs per request rather than on Identity's security-stamp interval because
    /// ARCHITECTURE.md §7 promises revocation, and revocation that takes effect in thirty
    /// minutes is not revocation. It costs one indexed single-row query on a joined key,
    /// and it is the reason the session row carries no <c>last_seen_at</c>: a read stays
    /// a read.
    /// </remarks>
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sessionClaim = context.Principal?.FindFirstValue(IdentityClaimTypes.SessionId);
        if (!Guid.TryParse(sessionClaim, out var sessionId))
        {
            await RejectAsync(context).ConfigureAwait(false);
            return;
        }

        var services = context.HttpContext.RequestServices;
        var database = services.GetRequiredService<ItmsIdentityDbContext>();
        var now = services.GetRequiredService<IClock>().UtcNow;
        var cancellationToken = context.HttpContext.RequestAborted;

        // One query: the session and its owner's active flag. A revoked session, an
        // expired one, a deleted one, and a deactivated user are all the same answer.
        var state = await database.Sessions
            .AsNoTracking()
            .Where(session => session.Id == sessionId)
            .Join(
                database.Users.AsNoTracking(),
                session => session.UserId,
                user => user.Id,
                (session, user) => new
                {
                    session.RevokedAt,
                    session.ExpiresAt,
                    user.IsActive,
                })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (state is null || state.RevokedAt is not null || state.ExpiresAt <= now || !state.IsActive)
        {
            await RejectAsync(context).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Answers an unauthenticated request with 401 instead of a redirect to an HTML
    /// login page. This is an API: a 302 to markup would leave a fetch call parsing a
    /// login form as if it were data.
    /// </summary>
    /// <param name="context">The redirect the handler wanted to perform.</param>
    public static Task OnRedirectToLoginAsync(RedirectContext<CookieAuthenticationOptions> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Answers a forbidden request with 403, never a disguised 404 and never a redirect
    /// (ARCHITECTURE.md §6).
    /// </summary>
    /// <param name="context">The redirect the handler wanted to perform.</param>
    public static Task OnRedirectToAccessDeniedAsync(RedirectContext<CookieAuthenticationOptions> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();

        // Clearing the cookie as well means a revoked session stops costing a database
        // query on every subsequent request from that browser.
        await context.HttpContext
            .SignOutAsync(IdentityConstants.ApplicationScheme)
            .ConfigureAwait(false);
    }
}
