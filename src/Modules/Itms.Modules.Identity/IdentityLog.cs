using Microsoft.Extensions.Logging;

namespace Itms.Modules.Identity;

/// <summary>
/// The module's log messages, source-generated. CONVENTIONS.md requires structured
/// properties, and the repo builds warnings-as-errors with CA1848 on, so every message
/// is declared here rather than formatted at the call site.
/// </summary>
/// <remarks>
/// Nothing here ever takes a password, a hash, a cookie, or a token as a parameter.
/// A failed sign-in is logged by user name because that is what an operator needs to
/// correlate; the credential that failed is never written anywhere.
/// </remarks>
internal static partial class IdentityLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "User {UserId} signed in, session {SessionId}.")]
    public static partial void SignedIn(ILogger logger, Guid userId, Guid sessionId);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Failed sign-in for {UserName}: {Reason}.")]
    public static partial void SignInFailed(ILogger logger, string userName, string reason);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Session {SessionId} revoked ({Reason}).")]
    public static partial void SessionRevoked(ILogger logger, Guid sessionId, string reason);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "User {UserId} changed their password; {RevokedCount} other session(s) revoked.")]
    public static partial void PasswordChanged(ILogger logger, Guid userId, int revokedCount);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Seeded development account {UserName} with role {Role}.")]
    public static partial void SeededAccount(ILogger logger, string userName, string role);
}
