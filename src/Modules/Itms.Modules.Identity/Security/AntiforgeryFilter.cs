using Itms.Platform.Http;
using Itms.Platform.Results;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace Itms.Modules.Identity.Security;

/// <summary>
/// Rejects an unsafe request that does not carry a valid antiforgery token
/// (CONVENTIONS.md: "CSRF protection enabled for cookie auth").
/// </summary>
/// <remarks>
/// Minimal APIs validate antiforgery automatically only for form-bound endpoints, and
/// these endpoints take JSON, so validation is explicit here. It matters most on
/// <c>/login</c>: without it a hostile page can silently sign a visitor into an account
/// the attacker controls, which is login CSRF and needs no existing session to work.
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
