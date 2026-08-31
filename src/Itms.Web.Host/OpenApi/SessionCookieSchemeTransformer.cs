using Itms.Modules.Identity.Security;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Itms.Web.Host.OpenApi;

/// <summary>
/// Declares the session cookie as the document's only security scheme.
/// </summary>
/// <remarks>
/// ARCHITECTURE.md §7 is explicit that there is no bearer token and nothing in browser
/// storage. A generated document that omitted the scheme entirely would leave a reader —
/// or a code generator — to guess at a header that does not exist.
/// </remarks>
internal sealed class SessionCookieSchemeTransformer(IOptions<ItmsAuthOptions> options)
    : IOpenApiDocumentTransformer
{
    /// <summary>The scheme's name in <c>components.securitySchemes</c>.</summary>
    public const string SchemeName = "sessionCookie";

    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);

        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = options.Value.CookieName,
            Description =
                "HttpOnly session cookie, issued by POST /api/v1/auth/login and revoked server-side "
                + "by POST /api/v1/auth/logout. The browser sends it automatically; it cannot be read "
                + "by script and is never held in browser storage.",
        };

        return Task.CompletedTask;
    }
}
