using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Persistence;

/// <summary>
/// The Directory module's context: its own schema, its own migrations history, and no
/// table any other module may read (ARCHITECTURE.md §4).
/// </summary>
/// <remarks>
/// It is always built on the connection <c>IModuleDbSession</c> hands out, never on a
/// pool of its own, so a change here and any outbox write that announces it commit in
/// one transaction.
/// </remarks>
/// <param name="options">Context options, built on the shared session connection.</param>
public sealed class DirectoryDbContext(DbContextOptions<DirectoryDbContext> options) : DbContext(options)
{
    /// <summary>The name of the schema this context owns.</summary>
    public const string SchemaName = "directory";

    /// <summary>The migrations history table, kept inside the directory schema.</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>The departments of the organisation.</summary>
    public DbSet<Department> Departments => Set<Department>();

    /// <summary>The location tree.</summary>
    public DbSet<Location> Locations => Set<Location>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfiguration(new DepartmentConfiguration());
        modelBuilder.ApplyConfiguration(new LocationConfiguration());
    }
}
