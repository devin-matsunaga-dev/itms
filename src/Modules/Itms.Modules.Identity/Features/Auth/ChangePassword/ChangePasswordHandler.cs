using Itms.Modules.Identity.Domain;
using Itms.Modules.Identity.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Itms.Modules.Identity.Features.Auth.ChangePassword;

/// <summary>
/// Changes the caller's own password and cuts every other session loose.
/// </summary>
/// <remarks>
/// The revocation is the point: a password is usually changed because the old one may
/// be known to someone else, and leaving that someone else's cookie working would make
/// the change cosmetic. The session the caller is using survives, so they are not
/// signed out of the browser they just used.
/// </remarks>
/// <param name="userManager">The user store, which owns hashing and the policy check.</param>
/// <param name="database">The identity context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="logger">Structured log.</param>
internal sealed class ChangePasswordHandler(
    UserManager<ItmsUser> userManager,
    ItmsIdentityDbContext database,
    IModuleDbSession session,
    IClock clock,
    ILogger<ChangePasswordHandler> logger)
{
    /// <summary>Changes the password of <paramref name="userId"/>.</summary>
    /// <param name="userId">The caller.</param>
    /// <param name="currentSessionId">The session to keep alive.</param>
    /// <param name="request">The old and new passwords.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success, or the policy failure mapped onto the <c>newPassword</c> field.</returns>
    public async Task<Result> HandleAsync(
        Guid userId,
        Guid currentSessionId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return Error.Unauthorized("auth.session_stale", "The signed-in account no longer exists.");
        }

        Error? failure = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                // The hash change and the revocations commit together: a crash between
                // them would otherwise leave the old password dead and its sessions alive.
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                var changed = await userManager
                    .ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword)
                    .ConfigureAwait(false);

                if (!changed.Succeeded)
                {
                    failure = ToError(changed);
                    return;
                }

                var others = await database.Sessions
                    .Where(candidate =>
                        candidate.UserId == userId &&
                        candidate.RevokedAt == null &&
                        candidate.Id != currentSessionId)
                    .ToListAsync(token)
                    .ConfigureAwait(false);

                var now = clock.UtcNow;
                foreach (var other in others)
                {
                    other.Revoke(now, "password_changed");
                }

                await database.SaveChangesAsync(token).ConfigureAwait(false);
                IdentityLog.PasswordChanged(logger, userId, others.Count);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is null ? Result.Success() : Result.Failure(failure);
    }

    private static Error ToError(IdentityResult result)
    {
        // PasswordMismatch is the caller getting their current password wrong; everything
        // else is the new password failing the policy, and belongs on that field.
        if (result.Errors.Any(error => string.Equals(error.Code, "PasswordMismatch", StringComparison.Ordinal)))
        {
            return Error.Validation(
                "auth.password_mismatch",
                "Your current password is incorrect.",
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["currentPassword"] = ["Your current password is incorrect."],
                });
        }

        return Error.Validation(
            "auth.password_rejected",
            "The new password does not meet the password policy.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["newPassword"] = [.. result.Errors.Select(error => error.Description)],
            });
    }
}
