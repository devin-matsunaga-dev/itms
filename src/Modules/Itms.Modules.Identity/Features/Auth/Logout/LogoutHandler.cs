using Itms.Modules.Identity.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Itms.Modules.Identity.Features.Auth.Logout;

/// <summary>
/// Ends one session. Revoking the row is the part that matters: clearing the cookie
/// only asks the browser to forget it, while the row is what the server checks.
/// </summary>
/// <param name="database">The identity context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="logger">Structured log.</param>
internal sealed class LogoutHandler(
    ItmsIdentityDbContext database,
    IModuleDbSession session,
    IClock clock,
    ILogger<LogoutHandler> logger)
{
    /// <summary>Revokes <paramref name="sessionId"/> if it is still live.</summary>
    /// <param name="sessionId">The session named in the caller's cookie.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success even when the session was already gone: signing out twice is not an error.</returns>
    public async Task<Result> HandleAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                var row = await database.Sessions
                    .FirstOrDefaultAsync(candidate => candidate.Id == sessionId, token)
                    .ConfigureAwait(false);

                if (row is null)
                {
                    return;
                }

                row.Revoke(clock.UtcNow, "logout");
                await database.SaveChangesAsync(token).ConfigureAwait(false);
                IdentityLog.SessionRevoked(logger, sessionId, "logout");
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
