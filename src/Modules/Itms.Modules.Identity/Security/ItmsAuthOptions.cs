namespace Itms.Modules.Identity.Security;

/// <summary>
/// The authentication settings a deployment is allowed to change. Bound from the
/// <c>Identity</c> configuration section; every value has a production-safe default, so
/// an empty section is a hardened configuration rather than an open one.
/// </summary>
public sealed class ItmsAuthOptions
{
    /// <summary>The configuration section these are bound from.</summary>
    public const string SectionName = "Identity";

    /// <summary>The name of the authentication cookie.</summary>
    public string CookieName { get; set; } = "itms.session";

    /// <summary>
    /// How long a cookie survives without use. Sliding: each request inside the window
    /// renews it (ARCHITECTURE.md §7).
    /// </summary>
    public TimeSpan CookieLifetime { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// The absolute ceiling on a session, however active it is. The sliding cookie can
    /// renew forever; this is what stops a stolen cookie doing the same.
    /// </summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Failed sign-ins before the account locks.</summary>
    public int MaxFailedAccessAttempts { get; set; } = 5;

    /// <summary>How long a locked account stays locked.</summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>The shortest password the system accepts.</summary>
    public int MinimumPasswordLength { get; set; } = 12;

    /// <summary>
    /// Requests one address may make to the credential endpoints inside
    /// <see cref="RateLimitWindow"/> (CONVENTIONS.md's security floor).
    /// </summary>
    public int RateLimitPermits { get; set; } = 20;

    /// <summary>The window the rate limit is counted over.</summary>
    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromMinutes(1);
}
