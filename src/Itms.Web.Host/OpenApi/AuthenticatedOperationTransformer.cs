using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Itms.Web.Host.OpenApi;

/// <summary>
/// Marks every operation behind an authorization policy as requiring the session cookie,
/// and gives it the 401 that the authentication middleware — not the handler — produces.
/// </summary>
/// <remarks>
/// Read from the endpoint's own metadata rather than from a list kept by hand: a new
/// endpoint is protected the moment it calls <c>RequireAuthorization</c>, and the document
/// should say so without anyone remembering to come here.
/// </remarks>
internal sealed class AuthenticatedOperationTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        var anonymous = metadata.OfType<IAllowAnonymous>().Any();
        var authorized = metadata.OfType<IAuthorizeData>().Any();

        if (anonymous || !authorized)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SessionCookieSchemeTransformer.SchemeName)] = [],
        });

        // The 401 comes from the cookie handler before any handler runs, so no endpoint
        // declares it with ProducesProblem and every protected one can return it.
        operation.Responses ??= [];
        if (!operation.Responses.ContainsKey("401"))
        {
            operation.Responses["401"] = new OpenApiResponse
            {
                Description = "No session cookie, or the session it named has expired or been revoked.",
            };
        }

        return Task.CompletedTask;
    }
}
