using System.Security.Claims;
using Itms.Contracts.Auditing;
using Itms.Modules.Identity.Auditing;
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
/// <param name="audit">The audit trail. Both outcomes are recorded (ARCHITECTURE.md §8).</param>
/// <param name="logger">Structured log.</param>
internal sealed class LoginHandler(
    UserManager<ItmsUser> userManager,
    SignInManager<ItmsUser> signInManager,
    IUserClaimsPrincipalFactory<ItmsUser> claimsFactory,
    ItmsIdentityDbContext database,
    IModuleDbSession session,
    IClock clock,
    IOptions<ItmsAuthOptions> options,
    IAuditWriter audit,
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
            await AuditFailureAsync(request.UserName, userId: null, "no such account", cancellationToken)
                .ConfigureAwait(false);
            return InvalidCredentials;
        }

        if (!user.IsActive)
        {
            IdentityLog.SignInFailed(logger, request.UserName, "account deactivated");
            await AuditFailureAsync(request.UserName, user.Id, "account deactivated", cancellationToken)
                .ConfigureAwait(false);
            return InvalidCredentials;
        }

        var signIn = await signInManager
            .CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (signIn.IsLockedOut)
        {
            IdentityLog.SignInFailed(logger, request.UserName, "locked out");
            await AuditFailureAsync(request.UserName, user.Id, "locked out", cancellationToken)
                .ConfigureAwait(false);

            // The one failure that is named. Lockout is a state the account holder has to
            // be able to understand, and the attacker who caused it already knows.
            return Error.Unauthorized(
                "auth.locked_out",
                "This account is temporarily locked because of repeated failed sign-ins. Try again later.");
        }

        if (!signIn.Succeeded)
        {
            IdentityLog.SignInFailed(logger, request.UserName, "bad password");
            await AuditFailureAsync(request.UserName, user.Id, "bad password", cancellationToken)
                .ConfigureAwait(false);
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

                // In the same transaction as the session it describes: a sign-in that
                // failed to open a session must not leave a row saying it succeeded.
                await audit.WriteAsync(
                    new AuditEntry(
                        IdentityAuditActions.LoginSucceeded,
                        IdentityAuditActions.UserEntityType,
                        user.Id.ToString(),
                        new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
                        {
                            // Which session this sign-in opened, so revoking one later can
                            // be traced back to the address and moment it was created.
                            ["sessionId"] = new(null, userSession.Id.ToString()),
                        }),
                    token).ConfigureAwait(false);
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

    /// <summary>Records a refused sign-in.</summary>
    /// <param name="submittedUserName">What the caller typed. Untrusted text; see the remarks.</param>
    /// <param name="userId">The account, when one was found. Null when nothing matched.</param>
    /// <param name="reason">Which check refused it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>
    /// <para>
    /// The submitted identifier is recorded even when no such account exists, because a
    /// run of failures against names that do not exist is what credential stuffing and
    /// account enumeration look like, and the trail is where that gets noticed. It is
    /// attacker-chosen text: it is length-capped on the way in and must be encoded by
    /// whatever displays it. The password is never recorded, here or anywhere.
    /// </para>
    /// <para>
    /// The reason is a field on the entry rather than part of the action name, so
    /// <c>auth.login_failed</c> stays one countable thing. It is never returned to the
    /// caller — the response says only that the credentials were wrong.
    /// </para>
    /// </remarks>
    private Task AuditFailureAsync(
        string submittedUserName,
        Guid? userId,
        string reason,
        CancellationToken cancellationToken) =>
        audit.WriteAsync(
            new AuditEntry(
                IdentityAuditActions.LoginFailed,
                IdentityAuditActions.UserEntityType,
                // The account when there is one, so the failures against a real account
                // sit beside its other history; otherwise the string that was tried.
                userId?.ToString() ?? submittedUserName,
                new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
                {
                    ["userName"] = new(null, submittedUserName),
                    ["reason"] = new(null, reason),
                }),
            cancellationToken);
}
