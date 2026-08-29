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

    private readonly SemaphoreSlim _startGate = new(1, 1);
    private NpgsqlDataSource? _dataSource;
    private Respawner? _respawner;

    /// <summary>The connection string of the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>The pooled data source tests build sessions and contexts on.</summary>
    public NpgsqlDataSource DataSource =>
        _dataSource ?? throw new InvalidOperationException($"Call {nameof(StartAsync)} first.");

    /// <summary>
    /// Starts the container and opens the data source. Idempotent, and safe to call
    /// from two fixtures at once: the container is shared by the whole assembly, so
    /// whichever fixture initialises first pays for it and the rest join.
    /// </summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_dataSource is not null)
            {
                return;
            }

            await _container.StartAsync(cancellationToken).ConfigureAwait(false);
            _dataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        }
        finally
        {
            _startGate.Release();
        }
    }

    /// <summary>
    /// Creates an additional database inside the same container and returns a data
    /// source for it.
    /// </summary>
    /// <param name="name">The database name. Letters, digits, and underscores only.</param>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>A pooled data source for the new database.</returns>
    /// <remarks>
    /// CONVENTIONS.md wants one container per assembly, but two suites cannot share one
    /// <em>database</em> when one of them boots the real host: the host runs the outbox
    /// dispatcher, which would claim the messages the outbox tests are asserting on. A
    /// second database in the same container costs nothing and removes the race.
    /// </remarks>
    public async Task<NpgsqlDataSource> CreateDatabaseAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            throw new ArgumentException("A database name may contain only letters, digits, and underscores.", nameof(name));
        }

        await using (var connection = await DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
        await using (var command = connection.CreateCommand())
        {
            // CREATE DATABASE takes no parameters, which is why the name is validated
            // above rather than passed as one.
            command.CommandText = $"CREATE DATABASE \"{name}\"";
            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (PostgresException exception) when (exception.SqlState == "42P04")
            {
                // Already there, from an earlier run against a reused container.
            }
        }

        return new NpgsqlDataSourceBuilder(ConnectionStringFor(name)).Build();
    }

    /// <summary>
    /// The connection string for another database in the same container.
    /// </summary>
    /// <param name="name">The database name.</param>
    /// <returns>A connection string, password included.</returns>
    /// <remarks>
    /// <see cref="NpgsqlDataSource.ConnectionString"/> has the password stripped out of
    /// it, so a caller that needs to hand a connection string to another process — a host
    /// booted by the test, for instance — has to build it from this rather than read it
    /// back off the data source.
    /// </remarks>
    public string ConnectionStringFor(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }

    /// <summary>
    /// Prepares a reset for a database other than the container's default one.
    /// </summary>
    /// <param name="dataSource">The data source to truncate.</param>
    /// <param name="schemas">The schemas to include.</param>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>A respawner the caller resets with between tests.</returns>
    public static async Task<Respawner> CreateRespawnerAsync(
        NpgsqlDataSource dataSource,
        string[] schemas,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(schemas);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = schemas,
                TablesToIgnore = ["__ef_migrations_history"],
            }).ConfigureAwait(false);
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
