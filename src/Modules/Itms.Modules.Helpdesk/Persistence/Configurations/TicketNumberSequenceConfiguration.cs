using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Helpdesk.Persistence.Configurations;

/// <summary>Maps <see cref="TicketNumberSequence"/> to <c>helpdesk.ticket_number_sequences</c>.</summary>
/// <remarks>
/// Applied directly rather than through a <c>DbSet</c>, which is what keeps the counter
/// out of reach of everything but <c>TicketNumberGenerator</c>'s one statement.
/// </remarks>
internal sealed class TicketNumberSequenceConfiguration : IEntityTypeConfiguration<TicketNumberSequence>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TicketNumberSequence> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TicketNumberSequence.TableName);
        builder.HasKey(s => s.Name).HasName("pk_ticket_number_sequences");

        builder
            .Property(s => s.Name)
            .HasColumnName("name")
            .HasMaxLength(TicketNumberSequence.NameMaxLength)
            .IsRequired();

        builder.Property(s => s.NextValue).HasColumnName("next_value").IsRequired();
    }
}
