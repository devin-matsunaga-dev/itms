using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Messaging.Outbox;

/// <summary>Maps <see cref="OutboxConsumption"/> to <c>messaging.outbox_consumptions</c>.</summary>
internal sealed class OutboxConsumptionConfiguration : IEntityTypeConfiguration<OutboxConsumption>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxConsumption> builder)
    {
        builder.ToTable("outbox_consumptions");

        // The composite key is the idempotency guarantee itself: a redelivered message
        // cannot produce a second consumption row, because the database will not have it.
        builder.HasKey(c => new { c.MessageId, c.ConsumerName }).HasName("pk_outbox_consumptions");

        builder.Property(c => c.MessageId).HasColumnName("message_id");
        builder.Property(c => c.ConsumerName).HasColumnName("consumer_name").HasMaxLength(512);
        builder.Property(c => c.ConsumedAt).HasColumnName("consumed_at").IsRequired();

        builder
            .HasOne<OutboxMessage>()
            .WithMany()
            .HasForeignKey(c => c.MessageId)
            .HasConstraintName("fk_outbox_consumptions_message")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
