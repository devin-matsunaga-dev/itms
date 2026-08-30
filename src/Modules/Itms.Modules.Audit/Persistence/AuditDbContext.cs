using Itms.Modules.Audit.Domain;
using Itms.Modules.Audit.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Audit.Persistence;

/// <summary>
/// The Audit module's context: its own schema, its own migrations history, and no table
/// any other module may read (ARCHITECTURE.md §4).
/// </summary>
/// <remarks>
/// <para>
/// It exposes no <c>DbSet</c>. A <c>DbSet&lt;AuditRecord&gt;</c> property would hand
/// every caller <c>Remove</c>, <c>RemoveRange</c>, <c>ExecuteDelete</c>, and
/// <c>ExecuteUpdate</c> on the audit table, and invariant 10 says none of those may
/// exist anywhere in this system. The one way in is <see cref="AppendAsync"/>; the one
/// way out, when WP-5.9 builds the viewer, will be <see cref="Query"/>.
/// </para>
/// <para>
/// Like every module context it is built on the connection <c>IModuleDbSession</c> hands
/// out, so an audited change and the row recording it commit in one transaction.
/// </para>
/// </remarks>
/// <param name="options">Context options, built on the shared session connection.</param>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    /// <summary>The name of the schema this context owns.</summary>
    public const string SchemaName = "audit";

    /// <summary>The migrations history table, kept inside the audit schema.</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>The audit table.</summary>
    public const string TableName = "audit_entries";

    /// <summary>A read-only, untracked view of the trail, for the viewer WP-5.9 builds.</summary>
    /// <returns>A query over the audit rows.</returns>
    public IQueryable<AuditRecord> Query() => Set<AuditRecord>().AsNoTracking();

    /// <summary>Stages <paramref name="record"/> for insertion. The only write this context offers.</summary>
    /// <param name="record">The row to append.</param>
    /// <param name="cancellationToken">Cancels the staging.</param>
    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await AddAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfiguration(new AuditRecordConfiguration());
    }
}
