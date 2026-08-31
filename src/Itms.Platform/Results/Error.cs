namespace Itms.Platform.Results;

/// <summary>
/// A failure a handler chose to return rather than throw. CONVENTIONS.md reserves
/// exceptions for the genuinely exceptional, so every expected failure — not found,
/// illegal transition, forbidden — travels as one of these inside a <see cref="Result"/>.
/// </summary>
/// <param name="Code">A stable, machine-readable identifier such as <c>ticket.illegal_transition</c>. Clients may switch on it; it must not be reworded casually.</param>
/// <param name="Message">A human-readable description, safe to show a user. Never contains secrets, SQL, or stack traces.</param>
/// <param name="Kind">How the endpoint layer should translate this failure to HTTP.</param>
/// <param name="FieldErrors">Per-field messages for <see cref="ErrorKind.Validation"/> failures, keyed by field name. Empty for every other kind.</param>
public sealed record Error(
    string Code,
    string Message,
    ErrorKind Kind,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    /// <summary>Input failed validation, without per-field detail.</summary>
    public static Error Validation(string code, string message) =>
        new(code, message, ErrorKind.Validation);

    /// <summary>Input failed validation, with per-field messages that the client maps back onto form fields.</summary>
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new(code, message, ErrorKind.Validation, fieldErrors);

    /// <summary>The entity does not exist.</summary>
    public static Error NotFound(string code, string message) =>
        new(code, message, ErrorKind.NotFound);

    /// <summary>The request conflicts with current state.</summary>
    public static Error Conflict(string code, string message) =>
        new(code, message, ErrorKind.Conflict);

    /// <summary>A precondition the caller stated — a stale <c>If-Match</c> — does not hold.</summary>
    public static Error PreconditionFailed(string code, string message) =>
        new(code, message, ErrorKind.PreconditionFailed);

    /// <summary>The caller is authenticated but not permitted.</summary>
    public static Error Forbidden(string code, string message) =>
        new(code, message, ErrorKind.Forbidden);

    /// <summary>The caller is not authenticated.</summary>
    public static Error Unauthorized(string code, string message) =>
        new(code, message, ErrorKind.Unauthorized);

    /// <summary>A failure the caller cannot act on.</summary>
    public static Error Unexpected(string code, string message) =>
        new(code, message, ErrorKind.Unexpected);
}
