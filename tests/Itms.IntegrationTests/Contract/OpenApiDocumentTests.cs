using System.Text.Json;
using Itms.IntegrationTests.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Itms.IntegrationTests.Contract;

/// <summary>
/// The document is the contract, and CONVENTIONS.md makes the client's types generated
/// from it. Anything the document fails to say is therefore something no client can know.
/// </summary>
/// <remarks>
/// Read from the running host rather than from the committed file, so these assert what
/// the application actually describes. The committed copy is kept honest separately: the
/// build rewrites it, so a change that was not committed shows up as a diff in CI.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class OpenApiDocumentTests(IdentityWebFixture fixture)
{
    private const string ApiPrefix = "/api/v1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_api_endpoint_the_host_maps_appears_in_the_document()
    {
        var described = (await DocumentAsync()).RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .Select(path => path.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missing = MappedApiRoutes().Where(route => !described.Contains(route)).ToList();

        // An endpoint the document omits is an endpoint the generated client cannot call.
        // The usual cause is a route mapped outside a group that carries the metadata.
        missing.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_document_describes_the_api_and_nothing_else()
    {
        var routes = (await DocumentAsync()).RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .Select(path => path.Name)
            .ToList();

        routes.ShouldNotBeEmpty();
        routes.ShouldAllBe(route => route.StartsWith(ApiPrefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Every operation carries an <c>operationId</c>, which is the name the generated
    /// client gives it. An unnamed one gets a name derived from its route, so adding a
    /// path segment would silently rename a client's call.
    /// </summary>
    [Fact]
    public async Task Every_operation_is_named()
    {
        var unnamed = new List<string>();

        foreach (var path in (await DocumentAsync()).RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!operation.Value.TryGetProperty("operationId", out var id)
                    || string.IsNullOrWhiteSpace(id.GetString()))
                {
                    unnamed.Add($"{operation.Name.ToUpperInvariant()} {path.Name}");
                }
            }
        }

        unnamed.ShouldBeEmpty();
    }

    /// <summary>
    /// The two RFC 7807 extensions <c>ProblemDetailsMapper</c> writes. They are extensions,
    /// so nothing but the document tells a client they exist — and the client matches on
    /// <c>code</c> rather than on message text precisely because the text will change.
    /// </summary>
    [Fact]
    public async Task The_problem_schema_declares_the_extensions_the_client_reads()
    {
        var properties = (await DocumentAsync()).RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("ProblemDetails")
            .GetProperty("properties");

        properties.TryGetProperty("code", out _).ShouldBeTrue();
        properties.TryGetProperty("errors", out _).ShouldBeTrue();
    }

    /// <summary>
    /// No schema anywhere in the document says a number may arrive as a string.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>JsonSerializerDefaults.Web</c> sets <c>AllowReadingFromString</c>, so the schema
    /// exporter widens every <c>int</c> to <c>["integer", "string"]</c> — describing what
    /// the parser tolerates rather than what the wire carries. <c>NumericSchemaTransformer</c>
    /// narrows them back, and this is what says so: a widened schema costs the generated
    /// client a <c>number | string</c> at every page number, total, and target.
    /// </para>
    /// <para>
    /// Parameter schemas are checked as well as component schemas, because paging arrives
    /// as query parameters and that is where the union was first noticed (WP-1.5).
    /// </para>
    /// </remarks>
    [Fact]
    public async Task No_number_in_the_contract_is_also_described_as_a_string()
    {
        var widened = new List<string>();
        Inspect((await DocumentAsync()).RootElement, string.Empty, widened);

        widened.ShouldBeEmpty();
    }

    /// <summary>Walks the document collecting the paths of any number-or-string schema.</summary>
    /// <param name="element">The node to inspect.</param>
    /// <param name="path">Where <paramref name="element"/> sits, for the failure message.</param>
    /// <param name="widened">Collects one entry per offending schema.</param>
    private static void Inspect(JsonElement element, string path, List<string> widened)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("type", out var type) && IsNumericUnion(type))
                {
                    widened.Add(path);
                }

                foreach (var property in element.EnumerateObject())
                {
                    Inspect(property.Value, $"{path}/{property.Name}", widened);
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Inspect(item, $"{path}/{index++}", widened);
                }

                break;

            default:
                break;
        }
    }

    /// <summary>True when a schema's type list carries both a number and a string.</summary>
    /// <param name="type">The schema's <c>type</c> member, which may be a name or a list.</param>
    private static bool IsNumericUnion(JsonElement type)
    {
        if (type.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var names = type.EnumerateArray()
            .Where(name => name.ValueKind == JsonValueKind.String)
            .Select(name => name.GetString())
            .ToList();

        return names.Contains("string", StringComparer.Ordinal)
            && (names.Contains("integer", StringComparer.Ordinal)
                || names.Contains("number", StringComparer.Ordinal));
    }

    /// <summary>
    /// ARCHITECTURE.md §7: the session cookie is the only credential, and there is no
    /// bearer token anywhere in this system.
    /// </summary>
    [Fact]
    public async Task Protected_operations_require_the_session_cookie()
    {
        using var document = await DocumentAsync();

        var schemes = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        var cookie = schemes.GetProperty("sessionCookie");
        cookie.GetProperty("type").GetString().ShouldBe("apiKey");
        cookie.GetProperty("in").GetString().ShouldBe("cookie");

        // /auth/me is behind authorization and is the shell's first call, so if any
        // operation carries the requirement it is this one.
        document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/auth/me")
            .GetProperty("get")
            .GetProperty("security")
            .EnumerateArray()
            .ShouldNotBeEmpty();
    }

    /// <summary>The routes the host actually maps under the API prefix.</summary>
    /// <remarks>
    /// A group's collection endpoint is mapped as <c>"/"</c>, which leaves a trailing
    /// slash on the raw pattern that routing ignores and the document does not carry, so
    /// both ends are trimmed before comparing.
    /// </remarks>
    private IEnumerable<string> MappedApiRoutes() =>
        fixture.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            // An endpoint excluded from the description is excluded deliberately; the
            // companion test asserts nothing outside the API prefix is described at all.
            .Where(endpoint => endpoint.Metadata.GetMetadata<IExcludeFromDescriptionMetadata>() is null)
            .Select(endpoint => "/" + endpoint.RoutePattern.RawText?.Trim('/'))
            .Where(route => route.StartsWith(ApiPrefix, StringComparison.Ordinal))
            .Select(NormalizeConstraints)
            .Distinct(StringComparer.Ordinal);

    /// <summary>
    /// Drops route constraints, so <c>{id:guid}</c> compares against the document's
    /// <c>{id}</c>. OpenAPI carries the constraint as the parameter's schema instead.
    /// </summary>
    private static string NormalizeConstraints(string route)
    {
        var segments = route.Split('/');

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var colon = segment.IndexOf(':', StringComparison.Ordinal);

            if (segment.StartsWith('{') && colon >= 0)
            {
                segments[i] = string.Concat(segment.AsSpan(0, colon), "}");
            }
        }

        return string.Join('/', segments);
    }

    private async Task<JsonDocument> DocumentAsync()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), Token);
        response.EnsureSuccessStatusCode();

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
    }
}
