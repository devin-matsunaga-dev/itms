using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Itms.Modules.Monitoring.Persistence;

/// <summary>
/// Builds a <see cref="MonitoringDbContext"/> for <c>dotnet ef migrations</c>, which runs
/// with no host and therefore no Aspire connection string.
/// </summary>
/// <remarks>
/// The connection string here is never opened: scaffolding only needs the provider to know
/// it is PostgreSQL. It points at nothing real on purpose, so a mistyped command cannot
/// reach a database that matters.
/// </remarks>
public sealed class MonitoringDbContextFactory : IDesignTimeDbContextFactory<MonitoringDbContext>
{
    /// <inheritdoc />
    public MonitoringDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MonitoringDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=itms_design_time;Username=postgres",
                npgsql => npgsql.MigrationsHistoryTable(
                    MonitoringDbContext.MigrationsHistoryTable,
                    MonitoringDbContext.SchemaName))
            .Options;

        return new MonitoringDbContext(options);
    }
}
