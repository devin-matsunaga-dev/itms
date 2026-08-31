using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Itms.Web.Host.OpenApi;

/// <summary>
/// Adds the two members <c>ProblemDetailsMapper</c> writes as RFC 7807 extensions.
/// </summary>
/// <remarks>
/// The framework's <see cref="ProblemDetails"/> carries its extensions in a dictionary, so
/// the inferred schema says only "some additional properties" — and a generated client
/// would have to reach for them untyped. Both are load-bearing: the client matches on
/// <c>code</c> rather than on message text, and maps <c>errors</c> straight onto form
/// fields. Declaring them here is what keeps that typed.
/// </remarks>
internal sealed class ProblemDetailsSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        if (context.JsonTypeInfo.Type != typeof(ProblemDetails)
            && context.JsonTypeInfo.Type != typeof(HttpValidationProblemDetails))
        {
            return Task.CompletedTask;
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

        // The framework describes a nullable int32 as integer-or-string, because a JSON
        // number that large may arrive quoted. A status code never does, and leaving it
        // would hand the client a `number | string` it would have to narrow at every use.
        schema.Properties["status"] = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer | JsonSchemaType.Null,
            Format = "int32",
            Description = "The HTTP status code, repeated in the body as RFC 7807 allows.",
        };

        schema.Properties["code"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description =
                "The machine-readable error identifier, e.g. auth.antiforgery_failed. Present on "
                + "every problem this system produces itself; absent on one produced by the framework.",
        };

        if (context.JsonTypeInfo.Type == typeof(HttpValidationProblemDetails))
        {
            return Task.CompletedTask;
        }

        schema.Properties["errors"] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object | JsonSchemaType.Null,
            Description =
                "Per-field validation messages, keyed by camel-cased field name. Present only on a "
                + "validation failure.",
            AdditionalProperties = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = new OpenApiSchema { Type = JsonSchemaType.String },
            },
        };

        return Task.CompletedTask;
    }
}
