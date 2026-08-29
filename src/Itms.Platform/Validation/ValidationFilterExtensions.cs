using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Itms.Platform.Validation;

/// <summary>Attaches <see cref="ValidationFilter{TRequest}"/> to an endpoint.</summary>
public static class ValidationFilterExtensions
{
    /// <summary>
    /// Validates <typeparamref name="TRequest"/> before the handler runs and documents
    /// the 400 in OpenAPI, so the generated client knows the shape of a validation
    /// failure without anyone hand-writing it.
    /// </summary>
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesValidationProblem();
    }
}
