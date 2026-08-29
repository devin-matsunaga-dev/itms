using Itms.Messaging;
using Itms.Messaging.Outbox;
using Itms.Platform.Time;
using Itms.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Itms.IntegrationTests.Messaging;

/// <summary>
/// One PostgreSQL container and one migrated schema for every outbox test in this
/// assembly, per CONVENTIONS.md. Each test gets a truncated database and its own
/// service provider, so nothing leaks between them except the container itself.
/// </summary>
public sealed class OutboxFixture : IAsyncLifetime
{
    /// <summary>The schema the test consumers write their observable side effects into.</summary>
    public const string EffectsSchema = "test_effects";

    // Shared with every other fixture in this assembly; see SharedPostgres.
    private readonly PostgresDatabase _database = SharedPostgres.Instance;

    /// <summary>The pooled data source every session in a test is built on.</summary>
    public NpgsqlDataSource DataSource => _database.DataSource;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync(TestContext.Current.CancellationToken);

        await using (var provider = BuildProvider(new FakeClock()))
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<OutboxDbContext>().Database
                .MigrateAsync(TestContext.Current.CancellationToken);
        }

        await CreateEffectsTableAsync();

        // Respawn reads the table graph now, so both schemas must already exist.
        await _database.InitializeRespawnAsync(
            [OutboxDbContext.SchemaName, EffectsSchema],
            TestContext.Current.CancellationToken);
    }

    /// <summary>Empties the outbox and the effects table between tests.</summary>
    public Task ResetAsync() => _database.ResetAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// Builds a container for one test. The clock is injected so backoff can be waited
    /// out by moving time rather than by sleeping.
    /// </summary>
    /// <param name="clock">The test's clock.</param>
    /// <param name="configure">Optional overrides — batch size, attempt cap.</param>
    /// <returns>A provider registered with the bus and this assembly's test consumers.</returns>
    public ServiceProvider BuildProvider(IClock clock, Action<MessagingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var services = new ServiceCollection();

        services.AddSingleton(DataSource);
        services.AddSingleton(clock);
        services.AddSingleton<ConsumerScript>();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.Configure<MessagingOptions>(options => configure?.Invoke(options));

        // AddMessagingCore rather than AddMessaging: the background dispatcher would
        // race the assertions, so the tests drive IOutboxProcessor a pass at a time.
        services.AddMessagingCore(typeof(OutboxFixture).Assembly);

        return services.BuildServiceProvider(validateScopes: true);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The container is shared by the assembly and reaped when the process exits, so
    /// this fixture does not dispose it out from under the others.
    /// </remarks>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task CreateEffectsTableAsync()
    {
        // A consumer's side effect has to be visible in the database for the
        // exactly-once claim to mean anything: counting in-memory invocations would
        // pass even if the consumption row and the effect were committed separately.
        await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE SCHEMA IF NOT EXISTS {EffectsSchema};
            CREATE TABLE IF NOT EXISTS {EffectsSchema}.effects (
                id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                consumer    text        NOT NULL,
                event_id    uuid        NOT NULL,
                created_at  timestamptz NOT NULL DEFAULT now()
            );
            CREATE TABLE IF NOT EXISTS {EffectsSchema}.tickets (
                id      uuid PRIMARY KEY,
                subject text NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}

/// <summary>
/// Shares one container across every outbox test class, per CONVENTIONS.md's rule that
/// a container is started once per assembly rather than once per test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class OutboxTestGroup : ICollectionFixture<OutboxFixture>
{
    /// <summary>The collection name test classes attach to.</summary>
    public const string Name = "outbox";
}
