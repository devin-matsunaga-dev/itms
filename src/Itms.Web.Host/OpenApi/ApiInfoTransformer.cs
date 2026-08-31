using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Itms.Web.Host.OpenApi;

/// <summary>
/// Names the document.
/// </summary>
/// <remarks>
/// Without this the title is the assembly name, which tells a reader of the committed
/// contract nothing about what they are looking at or which version of the API it is.
/// </remarks>
internal sealed class ApiInfoTransformer : IOpenApiDocumentTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Info.Title = "ITMS API";
        document.Info.Version = OpenApiRegistration.DocumentName;
        document.Info.Description =
            "Unified IT Management System. Every route is versioned in its path (ARCHITECTURE.md §6), "
            + "every error is an RFC 7807 problem document, and authentication is the session cookie.";

        return Task.CompletedTask;
    }
}
