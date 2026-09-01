using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Persistence;

/// <summary>
/// The Assets module's context: its own schema, its own migrations history, and no table
/// any other module may read (ARCHITECTURE.md §4).
/// </summary>
/// <remarks>
/// It is always built on the connection <c>IModuleDbSession</c> hands out, never on a pool
/// of its own, so a change here and any outbox write that announces it commit in one
/// transaction.
/// </remarks>
/// <param name="options">Context options, built on the shared session connection.</param>
public sealed class AssetsDbContext(DbContextOptions<AssetsDbContext> options) : DbContext(options)
{
    /// <summary>The name of the schema this context owns.</summary>
    public const string SchemaName = "assets";

    /// <summary>The migrations history table, kept inside the assets schema.</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>The equipment records themselves.</summary>
    public DbSet<Asset> Assets => Set<Asset>();

    /// <summary>What kind of thing an asset is.</summary>
    public DbSet<AssetType> AssetTypes => Set<AssetType>();

    /// <summary>Where an asset is in its life.</summary>
    public DbSet<AssetStatus> AssetStatuses => Set<AssetStatus>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfiguration(new AssetTypeConfiguration());
        modelBuilder.ApplyConfiguration(new AssetStatusConfiguration());
        modelBuilder.ApplyConfiguration(new AssetConfiguration());
    }
}
