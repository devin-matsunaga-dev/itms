using FluentValidation;
using Itms.Platform.Http;
using Itms.Platform.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Itms.Platform.Validation;

/// <summary>
/// Runs the registered <see cref="IValidator{T}"/> before the handler, so handlers can
/// assume valid input (CONVENTIONS.md). A failure short-circuits to a 400
/// <c>ValidationProblemDetails</c> and the handler never runs.
/// </summary>
/// <typeparam name="TRequest">The request model bound from the body or the route.</typeparam>
public sealed class ValidationFilter<TRequest> : IEndpointFilter
    where TRequest : class
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.Arguments.OfType<TRequest>().FirstOrDefault() is not { } request)
        {
            return ProblemDetailsMapper.ToProblem(
                Error.Validation("request.missing", "A request body of the expected shape is required."));
        }

        // A missing validator is not an error: not every request model needs rules, and
        // failing closed here would make adding an endpoint a two-step affair.
        var validator = context.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
        if (validator is null)
        {
            return await next(context);
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (result.IsValid)
        {
            return await next(context);
        }

        var fieldErrors = result.Errors
            .GroupBy(failure => ToClientFieldName(failure.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        return ProblemDetailsMapper.ToProblem(
            Error.Validation("validation.failed", "One or more fields are invalid.", fieldErrors));
    }

    /// <summary>
    /// FluentValidation reports the CLR property name; the client sees camelCase JSON
    /// and maps errors back onto fields by that name. Converting here means no form has
    /// to translate keys.
    /// </summary>
    private static string ToClientFieldName(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return propertyName;
        }

        // Nested paths ("Address.Line1") camel-case each segment; indexers are left alone.
        var segments = propertyName.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.Length > 0 && char.IsUpper(segment[0]))
            {
                segments[i] = char.ToLowerInvariant(segment[0]) + segment[1..];
            }
        }

        return string.Join('.', segments);
    }
}
