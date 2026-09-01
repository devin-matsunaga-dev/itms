using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Itms.Modules.Assets.Persistence;

/// <summary>
/// Builds an <see cref="AssetsDbContext"/> for <c>dotnet ef migrations</c>, which runs
/// with no host and therefore no Aspire connection string.
/// </summary>
/// <remarks>
/// The connection string here is never opened: scaffolding only needs the provider to know
/// it is PostgreSQL. It points at nothing real on purpose, so a mistyped command cannot
/// reach a database that matters.
/// </remarks>
public sealed class AssetsDbContextFactory : IDesignTimeDbContextFactory<AssetsDbContext>
{
    /// <inheritdoc />
    public AssetsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AssetsDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=itms_design_time;Username=postgres",
                npgsql => npgsql.MigrationsHistoryTable(
                    AssetsDbContext.MigrationsHistoryTable,
                    AssetsDbContext.SchemaName))
            .Options;

        return new AssetsDbContext(options);
    }
}
