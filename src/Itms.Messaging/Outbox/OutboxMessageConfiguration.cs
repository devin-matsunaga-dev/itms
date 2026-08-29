using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Messaging.Outbox;

/// <summary>Maps <see cref="OutboxMessage"/> to <c>messaging.outbox_messages</c>.</summary>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id).HasName("pk_outbox_messages");
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(m => m.EventType).HasColumnName("event_type").HasMaxLength(512).IsRequired();
        builder.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.AvailableAt).HasColumnName("available_at").IsRequired();
        builder.Property(m => m.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(m => m.ProcessedAt).HasColumnName("processed_at");
        builder.Property(m => m.FailedAt).HasColumnName("failed_at");
        builder.Property(m => m.LastError).HasColumnName("last_error").HasMaxLength(2000);

        // The dispatcher's claim predicate, and the only query that runs on a hot path.
        // Filtered so the index holds outstanding work only: once a message is processed
        // it leaves the index, which keeps it small no matter how large the table grows.
        builder
            .HasIndex(m => m.AvailableAt)
            .HasDatabaseName("ix_outbox_messages_available")
            .HasFilter("processed_at IS NULL AND failed_at IS NULL");
    }
}
