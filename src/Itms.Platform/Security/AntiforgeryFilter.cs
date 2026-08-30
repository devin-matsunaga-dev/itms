using Itms.Platform.Http;
using Itms.Platform.Results;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace Itms.Platform.Security;

/// <summary>
/// Rejects an unsafe request that does not carry a valid antiforgery token
/// (CONVENTIONS.md: "CSRF protection enabled for cookie auth").
/// </summary>
/// <remarks>
/// <para>
/// Minimal APIs validate antiforgery automatically only for form-bound endpoints, and
/// every endpoint in this system takes JSON, so validation has to be explicit. Every
/// module that writes state behind a cookie needs it, which is why it lives in the
/// shared kernel rather than in the module that happened to need it first.
/// </para>
/// <para>
/// The token itself is configured and issued by the Identity module, which owns
/// authentication. This filter only asks the framework whether the request carries a
/// valid one.
/// </para>
/// </remarks>
/// <param name="antiforgery">The framework's token validator.</param>
public sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext).ConfigureAwait(false);
        }
        catch (AntiforgeryValidationException)
        {
            // Deliberately terse: the exception message describes which half of the token
            // pair failed, which is a hint no caller needs and an attacker would enjoy.
            return ProblemDetailsMapper.ToProblem(Error.Validation(
                "auth.antiforgery_failed",
                "The request did not carry a valid antiforgery token. Fetch one from /api/v1/auth/csrf and send it in the request header."));
        }

        return await next(context).ConfigureAwait(false);
    }
}
