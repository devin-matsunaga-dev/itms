using Itms.Modules.Helpdesk.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Helpdesk.Persistence.Configurations;

/// <summary>Maps <see cref="TicketPriority"/> to <c>helpdesk.ticket_priorities</c>.</summary>
internal sealed class TicketPriorityConfiguration : IEntityTypeConfiguration<TicketPriority>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TicketPriority> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ticket_priorities");
        builder.HasKey(p => p.Id).HasName("pk_ticket_priorities");

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.Code).HasColumnName("code").HasMaxLength(PriorityCode.MaxLength).IsRequired();
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(TicketPriority.NameMaxLength).IsRequired();
        builder.Property(p => p.NormalizedName).HasColumnName("normalized_name").HasMaxLength(TicketPriority.NameMaxLength).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(TicketPriority.DescriptionMaxLength);
        builder.Property(p => p.Rank).HasColumnName("rank").IsRequired();
        builder.Property(p => p.ResponseTargetMinutes).HasColumnName("response_target_minutes").IsRequired();
        builder.Property(p => p.ResolutionTargetMinutes).HasColumnName("resolution_target_minutes").IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");

        // The code is the key colours, integrations, and later rules resolve against; a
        // second row claiming one would make every one of those ambiguous. It is already
        // lower-cased by the domain, so a plain unique index is case-insensitive enough.
        builder
            .HasIndex(p => p.Code)
            .IsUnique()
            .HasDatabaseName("ux_ticket_priorities_code");

        builder
            .HasIndex(p => p.NormalizedName)
            .IsUnique()
            .HasDatabaseName("ux_ticket_priorities_normalized_name");

        // rank is deliberately NOT unique, for the same reason sort_order is not on a
        // category: reordering is a swap, and the name breaks a tie deterministically.
        builder
            .HasIndex(p => new { p.IsActive, p.Rank, p.NormalizedName })
            .HasDatabaseName("ix_ticket_priorities_active_rank");
    }
}
