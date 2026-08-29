using Microsoft.EntityFrameworkCore;

namespace Itms.Messaging.Outbox;

/// <summary>
/// The outbox's own context, in its own schema with its own migrations history.
/// </summary>
/// <remarks>
/// The bus is not a module, so it does not take a module schema; ARCHITECTURE.md §4's
/// "one schema per module" is about ownership, and <c>messaging</c> is owned by the
/// infrastructure rather than by any business area. Keeping its migrations history
/// separate means a module migration and an outbox migration never contend.
/// </remarks>
/// <param name="options">Context options, always built on the shared <c>IDbSession</c> connection.</param>
public sealed class OutboxDbContext(DbContextOptions<OutboxDbContext> options) : DbContext(options)
{
    /// <summary>The name of the schema this context owns.</summary>
    public const string SchemaName = "messaging";

    /// <summary>The migrations history table, kept inside the messaging schema.</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>Published events awaiting delivery, and the history of those delivered.</summary>
    public DbSet<OutboxMessage> Messages => Set<OutboxMessage>();

    /// <summary>Which consumer has consumed which message.</summary>
    public DbSet<OutboxConsumption> Consumptions => Set<OutboxConsumption>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxConsumptionConfiguration());
    }
}
