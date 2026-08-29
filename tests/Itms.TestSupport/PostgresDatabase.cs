using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace Itms.TestSupport;

/// <summary>
/// One PostgreSQL container, shared by every test in an assembly.
/// </summary>
/// <remarks>
/// <para>
/// CONVENTIONS.md is explicit that a container per test is how these suites become
/// unusable, so the container starts once and <see cref="ResetAsync"/> truncates
/// between tests. Truncation is also far faster than re-running migrations, which is
/// the other tempting way to get a clean table.
/// </para>
/// <para>
/// The image tag is pinned to the same major version the AppHost runs (WP-0.2), so a
/// test cannot pass against a PostgreSQL the application never sees.
/// </para>
/// </remarks>
public sealed class PostgresDatabase : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17.6")
        .WithDatabase("itms_tests")
        .WithUsername("itms")
        .WithPassword("itms")
        .Build();

    private NpgsqlDataSource? _dataSource;
    private Respawner? _respawner;

    /// <summary>The connection string of the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>The pooled data source tests build sessions and contexts on.</summary>
    public NpgsqlDataSource DataSource =>
        _dataSource ?? throw new InvalidOperationException($"Call {nameof(StartAsync)} first.");

    /// <summary>Starts the container and opens the data source.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _container.StartAsync(cancellationToken).ConfigureAwait(false);
        _dataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
    }

    /// <summary>
    /// Prepares the between-test reset. Call once, after the schema exists — Respawn
    /// reads the table graph at this point, so a table created later is not covered.
    /// </summary>
    /// <param name="schemas">The schemas to truncate.</param>
    /// <param name="cancellationToken">Cancels the setup.</param>
    public async Task InitializeRespawnAsync(string[] schemas, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schemas);

        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        _respawner = await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = schemas,
                // The migrations history table is schema, not data. Truncating it would
                // make every test after the first believe the database is unmigrated.
                TablesToIgnore = ["__ef_migrations_history"],
            }).ConfigureAwait(false);
    }

    /// <summary>Empties every covered table, leaving the schema in place.</summary>
    /// <param name="cancellationToken">Cancels the reset.</param>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (_respawner is null)
        {
            throw new InvalidOperationException($"Call {nameof(InitializeRespawnAsync)} first.");
        }

        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await _respawner.ResetAsync(connection).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync().ConfigureAwait(false);
        }

        await _container.DisposeAsync().ConfigureAwait(false);
    }
}
