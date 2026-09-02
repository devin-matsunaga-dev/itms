using Itms.Modules.Monitoring.Domain;
using Itms.Modules.Monitoring.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Monitoring.Persistence;

/// <summary>
/// The Monitoring module's context: its own schema, its own migrations history, and no
/// table any other module may read (ARCHITECTURE.md §4).
/// </summary>
/// <remarks>
/// <para>
/// It is always built on the connection <c>IModuleDbSession</c> hands out, never on a pool
/// of its own, so a change here and any outbox write that announces it commit in one
/// transaction.
/// </para>
/// <para>
/// <b>Nothing prunes <see cref="CheckResults"/> yet.</b> ARCHITECTURE.md §4 keeps raw
/// results for <see cref="CheckResult.RetentionDays"/> days and rolls them up beyond that,
/// and <c>WP-3.4</c>'s hosted service is what does both. Until it lands the table only
/// grows — which is the same shape of gap as the unpruned processed outbox rows and the
/// never-swept expired sessions STATUS.md already records, and it wants the same hosted
/// service.
/// </para>
/// </remarks>
/// <param name="options">Context options, built on the shared session connection.</param>
public sealed class MonitoringDbContext(DbContextOptions<MonitoringDbContext> options) : DbContext(options)
{
    /// <summary>The name of the schema this context owns.</summary>
    public const string SchemaName = "monitoring";

    /// <summary>The migrations history table, kept inside the monitoring schema.</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>The monitored devices — one per asset, at most (invariant 6).</summary>
    public DbSet<MonitoredDevice> Devices => Set<MonitoredDevice>();

    /// <summary>Every raw check the poller has reported. The only high-volume table in the system.</summary>
    public DbSet<CheckResult> CheckResults => Set<CheckResult>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfiguration(new MonitoredDeviceConfiguration());
        modelBuilder.ApplyConfiguration(new CheckResultConfiguration());
    }
}
