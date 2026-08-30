using Itms.Modules.Audit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Audit.Persistence.Configurations;

/// <summary>Maps <see cref="AuditRecord"/> to <c>audit.audit_entries</c>.</summary>
internal sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Persistence.AuditDbContext.TableName);
        builder.HasKey(a => a.Id).HasName("pk_audit_entries");

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(a => a.ActorId).HasColumnName("actor_id");
        builder.Property(a => a.ActorName).HasColumnName("actor_name").HasMaxLength(AuditRecord.ActorNameMaxLength);
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(AuditRecord.ActionMaxLength).IsRequired();
        builder.Property(a => a.EntityType).HasColumnName("entity_type").HasMaxLength(AuditRecord.EntityTypeMaxLength).IsRequired();
        builder.Property(a => a.EntityId).HasColumnName("entity_id").HasMaxLength(AuditRecord.EntityIdMaxLength).IsRequired();
        builder.Property(a => a.SourceIp).HasColumnName("source_ip").HasMaxLength(AuditRecord.SourceIpMaxLength);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();

        // jsonb rather than text, so WP-5.9's viewer can filter on a field name without
        // a second table and without parsing every row it reads.
        builder.Property(a => a.Changes).HasColumnName("changes").HasColumnType("jsonb");

        // The trail is read two ways: "what happened to this thing" and "what happened
        // in this window". There is no third, and an index nobody uses is a write cost.
        builder
            .HasIndex(a => new { a.EntityType, a.EntityId, a.OccurredAt })
            .HasDatabaseName("ix_audit_entries_entity");

        // Not declared descending: PostgreSQL scans a single-column btree backwards just
        // as fast, and the descending form only makes EF emit an array the analyzers then
        // object to in a file ARCHITECTURE.md §4 says is never edited after merge.
        builder
            .HasIndex(a => a.OccurredAt)
            .HasDatabaseName("ix_audit_entries_occurred_at");

        // "Everything this person did", which is the question an investigation starts
        // with. Partial, because a system-generated row has no actor to look up by.
        builder
            .HasIndex(a => new { a.ActorId, a.OccurredAt })
            .HasFilter("actor_id IS NOT NULL")
            .HasDatabaseName("ix_audit_entries_actor");
    }
}
