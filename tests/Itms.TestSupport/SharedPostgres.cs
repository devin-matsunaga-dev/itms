namespace Itms.TestSupport;

/// <summary>
/// The one PostgreSQL container an integration-test assembly gets.
/// </summary>
/// <remarks>
/// CONVENTIONS.md is explicit that a container per test is how these suites become
/// unusable, and the same reasoning applies to a container per fixture. Every fixture
/// takes this instance and calls <see cref="PostgresDatabase.StartAsync"/>, which is
/// idempotent; the container is removed by the Testcontainers resource reaper when the
/// test process exits, so nothing here owns its disposal.
/// </remarks>
public static class SharedPostgres
{
    /// <summary>The container every fixture in the assembly shares.</summary>
    public static PostgresDatabase Instance { get; } = new();
}
