using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Itms.Modules.Identity.Persistence;

/// <summary>
/// Builds an <see cref="ItmsIdentityDbContext"/> for <c>dotnet ef migrations</c>, which
/// runs with no host and therefore no Aspire connection string.
/// </summary>
/// <remarks>
/// The connection string here is never opened: scaffolding only needs the provider to
/// know it is PostgreSQL. It points at nothing real on purpose, so a mistyped command
/// cannot reach a database that matters.
/// </remarks>
public sealed class ItmsIdentityDbContextFactory : IDesignTimeDbContextFactory<ItmsIdentityDbContext>
{
    /// <inheritdoc />
    public ItmsIdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ItmsIdentityDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=itms_design_time;Username=postgres",
                npgsql => npgsql.MigrationsHistoryTable(
                    ItmsIdentityDbContext.MigrationsHistoryTable,
                    ItmsIdentityDbContext.SchemaName))
            .Options;

        return new ItmsIdentityDbContext(options);
    }
}
