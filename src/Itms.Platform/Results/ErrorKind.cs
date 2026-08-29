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

    /// <summary>The request conflicts with current state — an illegal state transition, a duplicate key, a stale ETag. Maps to 409.</summary>
    Conflict,

    /// <summary>The caller is authenticated but not permitted. Maps to 403 — never a disguised 404, per ARCHITECTURE.md §6.</summary>
    Forbidden,

    /// <summary>The caller is not authenticated. Maps to 401.</summary>
    Unauthorized,

    /// <summary>A failure the caller cannot act on. Maps to 500.</summary>
    Unexpected,
}
