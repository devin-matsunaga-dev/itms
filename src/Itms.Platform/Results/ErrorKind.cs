namespace Itms.Platform.Results;

/// <summary>
/// The kind of failure an <see cref="Error"/> represents. It exists so a handler can
/// say <em>what went wrong</em> without knowing anything about HTTP, and the endpoint
/// layer can translate that into a status code in one place
/// (<c>Itms.Platform.Http.ResultExtensions</c>) rather than in every endpoint.
/// </summary>
public enum ErrorKind
{
    /// <summary>Input failed validation. Maps to 400 with per-field errors.</summary>
    Validation,

    /// <summary>The requested entity does not exist, or the caller may not know that it does. Maps to 404.</summary>
    NotFound,

    /// <summary>The request conflicts with current state — an illegal state transition, a duplicate key, a write that lost a race. Maps to 409.</summary>
    Conflict,

    /// <summary>
    /// A condition the caller stated up front does not hold — a stale <c>If-Match</c>.
    /// Maps to 412.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Conflict"/> on purpose, and the distinction is about
    /// <em>when</em>: a 412 means the caller's own precondition stopped the request before
    /// anything was attempted, so nothing happened and nothing was half-done. A 409 means
    /// the work was attempted and lost. A client retrying the first needs only to reload;
    /// one retrying the second has to consider what else moved (WP-1.5).
    /// </remarks>
    PreconditionFailed,

    /// <summary>The caller is authenticated but not permitted. Maps to 403 — never a disguised 404, per ARCHITECTURE.md §6.</summary>
    Forbidden,

    /// <summary>The caller is not authenticated. Maps to 401.</summary>
    Unauthorized,

    /// <summary>A failure the caller cannot act on. Maps to 500.</summary>
    Unexpected,
}
