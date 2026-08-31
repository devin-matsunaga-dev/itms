using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Itms.Platform.Http;

/// <summary>
/// Turns a request the framework could not even read into a 400 <c>ProblemDetails</c>,
/// rather than the 500 an unhandled exception would otherwise produce.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> ARCHITECTURE.md §6 says errors are <c>ProblemDetails</c>
/// <em>always</em>. Model binding throws <see cref="BadHttpRequestException"/> before any
/// endpoint filter runs — a body that is not JSON, a number where an object belongs, a
/// value an enum does not have — so <c>ValidationFilter</c> never sees it and the
/// exception handler had nothing that recognised it. The caller got a 500 for a mistake
/// that was entirely theirs.
/// </para>
/// <para>
/// <b>The message is deliberately generic.</b> The framework's own text names the CLR
/// parameter and its type ("Failed to read parameter \"CreateLocationRequest request\"…"),
/// which tells an anonymous caller about internals they have no business seeing. The
/// status code is taken from the exception, which is what carries the framework's own
/// judgement — 400 for a body it could not parse, 413 for one too large.
/// </para>
/// <para>
/// This was found at WP-1.3, when the first endpoint taking an enum in its body was
/// tested with a value the enum does not have. It was never specific to that endpoint:
/// every module's request shapes had the same gap.
/// </para>
/// </remarks>
internal sealed class MalformedRequestExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    /// <summary>The error code a client matches on when it has sent something unreadable.</summary>
    public const string Code = "request.malformed";

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not BadHttpRequestException malformed)
        {
            return false;
        }

        var status = malformed.StatusCode is >= 400 and < 500
            ? malformed.StatusCode
            : StatusCodes.Status400BadRequest;

        httpContext.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                Title = ProblemDetailsMapper.TitleFor(status),
                Type = ProblemDetailsMapper.TypeFor(status),
                Detail = "The request could not be read. Check the shape and the values of the body.",
                Extensions = { ["code"] = Code },
            },
        }).ConfigureAwait(false);
    }
}
