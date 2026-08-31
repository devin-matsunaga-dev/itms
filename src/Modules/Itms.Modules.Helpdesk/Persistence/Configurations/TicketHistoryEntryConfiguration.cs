using Itms.Modules.Helpdesk.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Helpdesk.Persistence.Configurations;

/// <summary>Maps <see cref="TicketHistoryEntry"/> to <c>helpdesk.ticket_history</c>.</summary>
internal sealed class TicketHistoryEntryConfiguration : IEntityTypeConfiguration<TicketHistoryEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TicketHistoryEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ticket_history");
        builder.HasKey(entry => entry.Id).HasName("pk_ticket_history");

        builder.Property(entry => entry.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entry => entry.TicketId).HasColumnName("ticket_id").IsRequired();

        // Text, for the reason TicketChangeKind documents: this column is read at a psql
        // prompt during an incident.
        builder
            .Property(entry => entry.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder
            .Property(entry => entry.FromValue)
            .HasColumnName("from_value")
            .HasMaxLength(TicketHistoryEntry.ValueMaxLength);

        builder
            .Property(entry => entry.ToValue)
            .HasColumnName("to_value")
            .HasMaxLength(TicketHistoryEntry.ValueMaxLength);

        builder.Property(entry => entry.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(entry => entry.Sequence).HasColumnName("sequence").IsRequired();
        builder.Property(entry => entry.ActorId).HasColumnName("actor_id");

        builder
            .Property(entry => entry.ActorName)
            .HasColumnName("actor_name")
            .HasMaxLength(TicketHistoryEntry.ActorNameMaxLength);

        // A real foreign key with no navigation property, exactly as WP-1.2 mapped the
        // category and priority ones: §3 rule 6 forbids one only across a module boundary,
        // and CONVENTIONS.md's ban on lazy loading means a navigation would only ever be a
        // way to load an aggregate to render a list.
        //
        // RESTRICT rather than CASCADE, because a ticket delete is soft (ARCHITECTURE.md
        // §4) and no code path hard-deletes one. If a package ever adds one, it has to
        // decide what happens to the timeline deliberately rather than discover that the
        // database silently threw it away.
        builder
            .HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(entry => entry.TicketId)
            .HasConstraintName("fk_ticket_history_ticket_id")
            .OnDelete(DeleteBehavior.Restrict);

        // The only query this table serves: one ticket's timeline, newest first. Descending
        // on the instant and the ordinal so the index is read forwards rather than
        // backwards, in exactly the order ListTicketHistoryHandler asks for.
        builder
            .HasIndex(entry => new { entry.TicketId, entry.OccurredAt, entry.Sequence })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_ticket_history_ticket_id_occurred_at");
    }
}
