using Itms.Modules.Identity.Seeding;
using Itms.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;

namespace Itms.IntegrationTests.Identity;

/// <summary>
/// The real host, booted once for the whole authentication suite.
/// </summary>
/// <remarks>
/// WP-0.5's criteria are statements about HTTP — a 401 rather than a redirect to markup,
/// a 403 for the wrong role, a cookie that stops working the moment its session is
/// revoked — and none of them can be demonstrated against a hand-built service provider.
/// So this boots <c>Itms.Web.Host</c> itself, against its own database inside the
/// container the assembly already shares.
/// </remarks>
public sealed class IdentityWebFixture : IAsyncLifetime
{
    /// <summary>The database the host runs against, separate from the outbox suite's.</summary>
    public const string DatabaseName = "itms_web_tests";

    private readonly PostgresDatabase _container = SharedPostgres.Instance;
    private NpgsqlDataSource? _dataSource;
    private Respawner? _respawner;
    private IdentityWebApplicationFactory? _factory;

    /// <summary>The host's service provider, for arranging state a request cannot.</summary>
    public IServiceProvider Services =>
        _factory?.Services ?? throw new InvalidOperationException("The fixture has not been initialised.");

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await _container.StartAsync(cancellationToken);
        _dataSource = await _container.CreateDatabaseAsync(DatabaseName, cancellationToken);
        // Built from the container's string, not read back off the data source: Npgsql
        // strips the password out of NpgsqlDataSource.ConnectionString, and the host needs
        // one it can actually connect with.
        _factory = new IdentityWebApplicationFactory(_container.ConnectionStringFor(DatabaseName));

        // Creating a client is what builds and starts the host, which is what applies the
        // migrations and seeds. Respawn has to read the table graph after that.
        using (_factory.CreateClient())
        {
        }

        _respawner = await PostgresDatabase.CreateRespawnerAsync(
            _dataSource,
            ["identity", "messaging"],
            cancellationToken);
    }

    /// <summary>
    /// A client that talks to the host over https with its own cookie jar.
    /// </summary>
    /// <remarks>
    /// The base address is https because the session cookie is <c>Secure</c>: a client
    /// on http would be handed the cookie and then decline to send it back, and every
    /// test would fail for a reason that has nothing to do with authentication.
    /// Redirects are not followed, so a redirect-to-login regression shows up as a
    /// redirect rather than as whatever it points at.
    /// </remarks>
    /// <returns>A fresh client, representing one browser.</returns>
    public HttpClient CreateClient()
    {
        var factory = _factory ?? throw new InvalidOperationException("The fixture has not been initialised.");

        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    /// <summary>Empties the identity tables and re-seeds the development accounts.</summary>
    public async Task ResetAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var connection = await _dataSource!.OpenConnectionAsync(cancellationToken))
        {
            await _respawner!.ResetAsync(connection);
        }

        await using var scope = Services.CreateAsyncScope();
        await DevelopmentIdentitySeeder.SeedAsync(scope.ServiceProvider, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        // The container is shared by the assembly and reaped when the process exits.
    }

    private sealed class IdentityWebApplicationFactory(string connectionString) : WebApplicationFactory<Itms.Web.Host.AssemblyMarker>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // Development, because that is the environment in which the host applies its
            // own migrations and seeds the accounts the suite signs in as.
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:itms", connectionString);

            // Redis is registered by the host but nothing in these tests resolves it; the
            // client is lazy, so a connection string it never dials is enough.
            builder.UseSetting("ConnectionStrings:redis", "localhost:6379");

            // The rate limit is real and tested on its own; leaving it at the production
            // default here would make the suite's own volume of sign-ins trip it.
            builder.UseSetting("Identity:RateLimitPermits", "100000");
        }
    }
}

/// <summary>Shares one host across every authentication test class.</summary>
[CollectionDefinition(Name)]
public sealed class IdentityTestGroup : ICollectionFixture<IdentityWebFixture>
{
    /// <summary>The collection name test classes attach to.</summary>
    public const string Name = "identity-web";
}
