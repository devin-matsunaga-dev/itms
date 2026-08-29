using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Itms.Messaging.Outbox;

/// <summary>
/// Builds an <see cref="OutboxDbContext"/> for <c>dotnet ef migrations</c>, which runs
/// with no host and therefore no Aspire connection string.
/// </summary>
/// <remarks>
/// The connection string here is never opened: migration scaffolding only needs the
/// provider to know it is PostgreSQL. It points at nothing real on purpose, so that a
/// mistyped command cannot reach a database that matters.
/// </remarks>
public sealed class OutboxDbContextFactory : IDesignTimeDbContextFactory<OutboxDbContext>
{
    /// <inheritdoc />
    public OutboxDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OutboxDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=itms_design_time;Username=postgres",
                npgsql => npgsql.MigrationsHistoryTable(
                    OutboxDbContext.MigrationsHistoryTable,
                    OutboxDbContext.SchemaName))
            .Options;

        return new OutboxDbContext(options);
    }
}
