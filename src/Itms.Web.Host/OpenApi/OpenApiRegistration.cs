using Microsoft.AspNetCore.OpenApi;

namespace Itms.Web.Host.OpenApi;

/// <summary>
/// Registers the <c>v1</c> API document.
/// </summary>
/// <remarks>
/// The document is the contract: CONVENTIONS.md makes the React client's types generated
/// from it and a hand-written API type a review failure, so anything the document fails
/// to say is something the client cannot know. The transformers here exist to close the
/// gaps between what the framework infers from an endpoint and what this system actually
/// promises on the wire.
/// </remarks>
internal static class OpenApiRegistration
{
    /// <summary>The document name, which is also the file name and the route segment.</summary>
    public const string DocumentName = "v1";

    /// <summary>Adds the document and its transformers.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddItmsOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer<ApiInfoTransformer>();
            options.AddDocumentTransformer<SessionCookieSchemeTransformer>();
            options.AddOperationTransformer<AuthenticatedOperationTransformer>();

            // Order matters between these two: the numeric transformer narrows every
            // number-or-string union in the document, and the ProblemDetails one then
            // replaces `status` outright with a described schema of its own.
            options.AddSchemaTransformer<NumericSchemaTransformer>();
            options.AddSchemaTransformer<ProblemDetailsSchemaTransformer>();
        });

        return services;
    }
}
