namespace Itms.Modules.Identity.Domain;

/// <summary>
/// One sign-in. The cookie carries this row's id, and every request checks the row,
/// which is what makes "server-side session revocation" (ARCHITECTURE.md §7) true
/// rather than aspirational: a cookie whose session has been revoked is worthless the
/// instant the row changes, with no waiting for the cookie to expire.
/// </summary>
/// <remarks>
/// The row is deliberately not updated per request. A <c>last_seen_at</c> column would
/// turn every authenticated GET into a write, and the sliding expiry the cookie already
/// implements is what keeps an active session alive; <see cref="ExpiresAt"/> is the
/// absolute ceiling on top of it.
/// </remarks>
public sealed class UserSession
{
    private UserSession()
    {
    }

    /// <summary>The session id. Also the value of the session claim inside the cookie.</summary>
    public Guid Id { get; private set; }

    /// <summary>Whose session it is.</summary>
    public Guid UserId { get; private set; }

    /// <summary>When the user signed in (UTC).</summary>
    public DateTimeOffset IssuedAt { get; private set; }

    /// <summary>The absolute expiry. Past this instant the session is dead however recently it was used.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>When it was revoked, or <see langword="null"/> while it is live.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Why it was revoked — logout, password change, deactivation. Never a credential.</summary>
    public string? RevokedReason { get; private set; }

    /// <summary>The address the sign-in came from, for the audit trail (ARCHITECTURE.md §8).</summary>
    public string? IpAddress { get; private set; }

    /// <summary>The user agent at sign-in, truncated. Diagnostic only.</summary>
    public string? UserAgent { get; private set; }

    /// <summary>Opens a session.</summary>
    /// <param name="userId">Who signed in.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="lifetime">How long the session may live regardless of activity.</param>
    /// <param name="ipAddress">The caller's address, or <see langword="null"/> if the host cannot see one.</param>
    /// <param name="userAgent">The caller's user agent, or <see langword="null"/>.</param>
    /// <returns>The new session, not yet persisted.</returns>
    public static UserSession Open(
        Guid userId,
        DateTimeOffset now,
        TimeSpan lifetime,
        string? ipAddress,
        string? userAgent) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            IssuedAt = now,
            ExpiresAt = now + lifetime,
            IpAddress = Truncate(ipAddress, 45),
            UserAgent = Truncate(userAgent, 512),
        };

    /// <summary>True when the session may still authenticate a request.</summary>
    /// <param name="now">The current instant.</param>
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    /// <summary>Ends the session. Revoking an already-revoked session keeps the first reason.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="reason">Why — <c>logout</c>, <c>password_changed</c>, <c>deactivated</c>.</param>
    public void Revoke(DateTimeOffset now, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevokedReason = reason;
    }

    private static string? Truncate(string? value, int length) =>
        value is null || value.Length <= length ? value : value[..length];
}
