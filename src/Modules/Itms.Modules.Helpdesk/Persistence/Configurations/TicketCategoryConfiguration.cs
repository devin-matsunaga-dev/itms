using Itms.Modules.Helpdesk.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Helpdesk.Persistence.Configurations;

/// <summary>Maps <see cref="TicketCategory"/> to <c>helpdesk.ticket_categories</c>.</summary>
internal sealed class TicketCategoryConfiguration : IEntityTypeConfiguration<TicketCategory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TicketCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ticket_categories");
        builder.HasKey(c => c.Id).HasName("pk_ticket_categories");

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(TicketCategory.NameMaxLength).IsRequired();
        builder.Property(c => c.NormalizedName).HasColumnName("normalized_name").HasMaxLength(TicketCategory.NameMaxLength).IsRequired();
        builder.Property(c => c.Description).HasColumnName("description").HasMaxLength(TicketCategory.DescriptionMaxLength);
        builder.Property(c => c.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");

        // Two categories called "Network" would make every category report ambiguous, and
        // the case-insensitivity is what stops "network" being accepted as a second one.
        builder
            .HasIndex(c => c.NormalizedName)
            .IsUnique()
            .HasDatabaseName("ux_ticket_categories_normalized_name");

        // sort_order is deliberately NOT unique. Reordering a picker is a swap, and a
        // unique constraint turns a swap into a three-step dance that has to pass through
        // a value nobody asked for. Ties are harmless because the name breaks them, and
        // this index is the order every read uses.
        builder
            .HasIndex(c => new { c.IsActive, c.SortOrder, c.NormalizedName })
            .HasDatabaseName("ix_ticket_categories_active_order");
    }
}
