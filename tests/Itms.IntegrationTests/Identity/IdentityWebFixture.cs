using System.Net;
using Itms.Modules.Identity.Seeding;
using Itms.TestSupport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

    /// <summary>
    /// The address every request through this fixture appears to come from.
    /// </summary>
    /// <remarks>
    /// The in-memory transport has no socket, so it leaves
    /// <c>HttpContext.Connection.RemoteIpAddress</c> null where a real one never would.
    /// The host is given this address instead (see <c>RemoteAddressFilter</c>), so the
    /// audit tests can assert that the writer records the address the connection
    /// reports rather than merely that the column is nullable. It is from TEST-NET-3
    /// (RFC 5737), which is reserved for documentation and routes nowhere.
    /// </remarks>
    public const string RemoteIpAddress = "203.0.113.7";

    private readonly PostgresDatabase _container = SharedPostgres.Instance;
    private NpgsqlDataSource? _dataSource;
    private Respawner? _respawner;
    private IdentityWebApplicationFactory? _factory;

    /// <summary>The host's service provider, for arranging state a request cannot.</summary>
    public IServiceProvider Services =>
        _factory?.Services ?? throw new InvalidOperationException("The fixture has not been initialised.");

    /// <summary>
    /// The host's database, for assertions that must read past the application.
    /// </summary>
    /// <remarks>
    /// WP-0.7 needs it: an append-only table cannot be shown to be append-only by the
    /// code that has no way to change it, so the audit tests attack the table with plain
    /// SQL the way an operator at a psql prompt would.
    /// </remarks>
    public NpgsqlDataSource DataSource =>
        _dataSource ?? throw new InvalidOperationException("The fixture has not been initialised.");

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

        // Every module the host registers, not just Identity: the fixture boots the whole
        // composition root, so a schema left out here would leave one suite's rows behind
        // for the next one. WP-0.6 added "directory"; WP-0.7 added "audit".
        //
        // Respawn truncates, which is why the audit table's append-only trigger does not
        // block the reset: the trigger covers UPDATE and DELETE, and TRUNCATE needs table
        // ownership rather than write access.
        _respawner = await PostgresDatabase.CreateRespawnerAsync(
            _dataSource,
            ["identity", "messaging", "directory", "audit"],
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

    /// <summary>
    /// Empties every module's tables and re-seeds the development accounts.
    /// </summary>
    /// <remarks>
    /// The accounts come back because every suite signs in; the development directory
    /// deliberately does not, so a directory test starts from an empty tree and builds
    /// exactly the one it is asserting on. The seeder has its own test.
    /// </remarks>
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

            // Give the pipeline the one thing the in-memory transport cannot: a peer
            // address. Registered as a startup filter because the host is a minimal
            // WebApplication and this has to run ahead of everything it maps.
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter>(
                    new RemoteAddressFilter(IPAddress.Parse(RemoteIpAddress))));
        }
    }

    /// <summary>
    /// Stamps a peer address onto every request before the application sees it.
    /// </summary>
    /// <remarks>
    /// Only where there is none: this stands in for the socket the in-memory server does
    /// not have, and must never overwrite an address a real transport supplied.
    /// </remarks>
    private sealed class RemoteAddressFilter(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, proceed) =>
                {
                    context.Connection.RemoteIpAddress ??= address;
                    await proceed(context).ConfigureAwait(false);
                });

                next(app);
            };
    }
}

/// <summary>Shares one host across every authentication test class.</summary>
[CollectionDefinition(Name)]
public sealed class IdentityTestGroup : ICollectionFixture<IdentityWebFixture>
{
    /// <summary>The collection name test classes attach to.</summary>
    public const string Name = "identity-web";
}
