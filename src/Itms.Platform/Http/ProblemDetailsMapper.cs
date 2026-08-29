using Itms.Platform.Results;
using Microsoft.AspNetCore.Http;
// Aliased because Microsoft.AspNetCore.Http.Results collides with this project's
// Itms.Platform.Results namespace.
using MinimalApi = Microsoft.AspNetCore.Http.Results;
using Microsoft.AspNetCore.Mvc;

namespace Itms.Platform.Http;

/// <summary>
/// Turns an <see cref="Error"/> into an RFC 7807 payload. ARCHITECTURE.md §6 says
/// errors are <c>ProblemDetails</c> <em>always</em>, and the only way "always" survives
/// forty endpoints is for the translation to live in exactly one place.
/// </summary>
public static class ProblemDetailsMapper
{
    /// <summary>The HTTP status code an <paramref name="error"/> of each kind maps to.</summary>
    public static int StatusCodeFor(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };
    }

    /// <summary>
    /// The <c>type</c> URI for a status code. These point at the RFC that defines the
    /// status rather than at a documentation site this project would then have to keep
    /// alive.
    /// </summary>
    public static string TypeFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
        StatusCodes.Status401Unauthorized => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.2",
        StatusCodes.Status403Forbidden => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4",
        StatusCodes.Status404NotFound => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
        StatusCodes.Status409Conflict => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
        _ => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1",
    };

    /// <summary>A short, status-level title. The actionable text is in <c>detail</c>.</summary>
    public static string TitleFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "Internal Server Error",
    };

    /// <summary>
    /// Builds the response for <paramref name="error"/>. A validation failure carrying
    /// field errors becomes a <see cref="ValidationProblemDetails"/> so the client can
    /// map messages back onto form fields (CONVENTIONS.md, Forms).
    /// </summary>
    public static IResult ToProblem(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var status = StatusCodeFor(error);
        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = error.Code };

        if (error.Kind is ErrorKind.Validation && error.FieldErrors is { Count: > 0 })
        {
            return MinimalApi.ValidationProblem(
                errors: error.FieldErrors.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal),
                detail: error.Message,
                statusCode: status,
                title: TitleFor(status),
                type: TypeFor(status),
                extensions: extensions);
        }

        return MinimalApi.Problem(
            detail: error.Message,
            statusCode: status,
            title: TitleFor(status),
            type: TypeFor(status),
            extensions: extensions);
    }
}
