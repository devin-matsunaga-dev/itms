using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Itms.Modules.Helpdesk.Persistence;

/// <summary>
/// Builds a <see cref="HelpdeskDbContext"/> for <c>dotnet ef migrations</c>, which runs
/// with no host and therefore no Aspire connection string.
/// </summary>
/// <remarks>
/// The connection string here is never opened: scaffolding only needs the provider to
/// know it is PostgreSQL. It points at nothing real on purpose, so a mistyped command
/// cannot reach a database that matters.
/// </remarks>
public sealed class HelpdeskDbContextFactory : IDesignTimeDbContextFactory<HelpdeskDbContext>
{
    /// <inheritdoc />
    public HelpdeskDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=itms_design_time;Username=postgres",
                npgsql => npgsql.MigrationsHistoryTable(
                    HelpdeskDbContext.MigrationsHistoryTable,
                    HelpdeskDbContext.SchemaName))
            .Options;

        return new HelpdeskDbContext(options);
    }
}
