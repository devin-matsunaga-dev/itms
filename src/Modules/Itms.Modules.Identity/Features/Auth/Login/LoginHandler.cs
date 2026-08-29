using System.Security.Claims;
using Itms.Modules.Identity.Authorization;
using Itms.Modules.Identity.Domain;
using Itms.Modules.Identity.Persistence;
using Itms.Modules.Identity.Security;
using Itms.Platform.Data;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Itms.Modules.Identity.Features.Auth.Login;

/// <summary>The principal a successful sign-in produced, and the account it describes.</summary>
/// <param name="Principal">The claims principal to issue the cookie for.</param>
/// <param name="SessionId">The session row the cookie is bound to.</param>
/// <param name="User">The account, for the response body.</param>
internal sealed record LoginOutcome(ClaimsPrincipal Principal, Guid SessionId, AuthenticatedUserResponse User);

/// <summary>
/// Verifies credentials and opens a session. It does not issue the cookie — the
/// endpoint does that, so the handler stays free of <c>HttpContext</c>.
/// </summary>
/// <param name="userManager">The user store.</param>
/// <param name="signInManager">Password verification and lockout accounting.</param>
/// <param name="claimsFactory">Mints the principal.</param>
/// <param name="database">The identity context, built on the shared connection.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="options">Cookie and session lifetimes.</param>
/// <param name="logger">Structured log.</param>
internal sealed class LoginHandler(
    UserManager<ItmsUser> userManager,
    SignInManager<ItmsUser> signInManager,
    IUserClaimsPrincipalFactory<ItmsUser> claimsFactory,
    ItmsIdentityDbContext database,
    IModuleDbSession session,
    IClock clock,
    IOptions<ItmsAuthOptions> options,
    ILogger<LoginHandler> logger)
{
    // One message for "no such user", "wrong password", and "deactivated". Telling them
    // apart would turn the login form into a directory of who has an account here.
    private static readonly Error InvalidCredentials = Error.Unauthorized(
        "auth.invalid_credentials",
        "The user name or password is incorrect.");

    /// <summary>Signs the caller in, or explains why not.</summary>
    /// <param name="request">The offered credentials.</param>
    /// <param name="ipAddress">The caller's address, recorded on the session.</param>
    /// <param name="userAgent">The caller's user agent, recorded on the session.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The principal to issue a cookie for, or a failure.</returns>
    public async Task<Result<LoginOutcome>> HandleAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Either identifier signs in: people remember their address more reliably than
        // a user name, and both are unique.
        var user = await userManager.FindByNameAsync(request.UserName).ConfigureAwait(false)
            ?? await userManager.FindByEmailAsync(request.UserName).ConfigureAwait(false);

        if (user is null)
        {
            IdentityLog.SignInFailed(logger, request.UserName, "no such account");
            return InvalidCredentials;
        }

        if (!user.IsActive)
        {
            IdentityLog.SignInFailed(logger, request.UserName, "account deactivated");
            return InvalidCredentials;
        }

        var signIn = await signInManager
            .CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (signIn.IsLockedOut)
        {
            IdentityLog.SignInFailed(logger, request.UserName, "locked out");

            // The one failure that is named. Lockout is a state the account holder has to
            // be able to understand, and the attacker who caused it already knows.
            return Error.Unauthorized(
                "auth.locked_out",
                "This account is temporarily locked because of repeated failed sign-ins. Try again later.");
        }

        if (!signIn.Succeeded)
        {
            IdentityLog.SignInFailed(logger, request.UserName, "bad password");
            return InvalidCredentials;
        }

        var now = clock.UtcNow;
        var userSession = UserSession.Open(user.Id, now, options.Value.SessionLifetime, ipAddress, userAgent);

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);
                database.Sessions.Add(userSession);
                await database.SaveChangesAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        var principal = await claimsFactory.CreateAsync(user).ConfigureAwait(false);
        ((ClaimsIdentity)principal.Identity!).AddClaim(
            new Claim(IdentityClaimTypes.SessionId, userSession.Id.ToString()));

        var roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);

        IdentityLog.SignedIn(logger, user.Id, userSession.Id);

        return new LoginOutcome(
            principal,
            userSession.Id,
            new AuthenticatedUserResponse(
                user.Id,
                user.UserName!,
                user.Email!,
                user.DisplayName,
                [.. roles],
                user.DepartmentId,
                user.LocationId));
    }
}
