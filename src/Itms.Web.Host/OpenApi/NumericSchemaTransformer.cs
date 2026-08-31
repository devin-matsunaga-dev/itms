using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Itms.Web.Host.OpenApi;

/// <summary>
/// Narrows every numeric schema the framework describes as "number-or-string" back to the
/// number it actually is.
/// </summary>
/// <remarks>
/// <para>
/// Minimal APIs serialize with <c>JsonSerializerDefaults.Web</c>, which sets
/// <c>JsonNumberHandling.AllowReadingFromString</c> — so <c>System.Text.Json</c>'s schema
/// exporter, describing what the server would <em>accept</em>, emits
/// <c>{ "type": ["integer", "string"], "pattern": "^-?(?:0|[1-9]\d*)$" }</c> for every
/// <c>int</c> in the contract: page numbers, totals, SLA target minutes, byte lengths.
/// </para>
/// <para>
/// That is a true statement about the parser and a useless one about the wire. The server
/// never writes a quoted number, no client in this system sends one, and the union costs
/// the generated TypeScript a <c>number | string</c> that has to be narrowed at every use —
/// which WP-0.9 already refused once by hand for <c>ProblemDetails.status</c>. This is that
/// same fix, applied to the contract rather than to one property, so a paged or filtered
/// screen does not have to carry a workaround for a defect in how the contract is generated.
/// </para>
/// <para>
/// Nullability is preserved: a nullable <c>int</c> stays <c>["integer", "null"]</c>. If the
/// framework ever stops widening these, this transformer becomes redundant rather than wrong.
/// </para>
/// </remarks>
internal sealed class NumericSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);

        Narrow(schema);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Drops the <c>string</c> half — and the digit pattern that only exists to validate it —
    /// from a schema that is otherwise numeric.
    /// </summary>
    /// <param name="schema">The schema to narrow. Left alone unless it is a numeric union.</param>
    private static void Narrow(OpenApiSchema schema)
    {
        if (schema.Type is not { } type || !type.HasFlag(JsonSchemaType.String))
        {
            return;
        }

        var numeric = type & (JsonSchemaType.Integer | JsonSchemaType.Number);
        if (numeric == 0)
        {
            return;
        }

        schema.Type = numeric | (type & JsonSchemaType.Null);

        // The pattern describes the quoted form. With the string half gone it can only ever
        // be evaluated against a number, which is neither meaningful nor free.
        schema.Pattern = null;
    }
}
