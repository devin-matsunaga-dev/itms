using Itms.Modules.Assets.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Assets.Persistence.Configurations;

/// <summary>Maps <see cref="AssetType"/> to <c>assets.asset_types</c>.</summary>
internal sealed class AssetTypeConfiguration : IEntityTypeConfiguration<AssetType>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AssetType> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("asset_types");
        builder.HasKey(t => t.Id).HasName("pk_asset_types");

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(AssetType.NameMaxLength).IsRequired();
        builder.Property(t => t.NormalizedName).HasColumnName("normalized_name").HasMaxLength(AssetType.NameMaxLength).IsRequired();
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(AssetType.DescriptionMaxLength);
        builder.Property(t => t.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");

        // Two types called "Laptop" would make every asset report ambiguous, and the
        // case-insensitivity is what stops "laptop" being accepted as a second one.
        builder
            .HasIndex(t => t.NormalizedName)
            .IsUnique()
            .HasDatabaseName("ux_asset_types_normalized_name");

        // sort_order is deliberately NOT unique, for the reason WP-1.1 gave: reordering a
        // picker is a swap, and a unique constraint turns a swap into a three-step dance
        // that has to pass through a value nobody asked for. The name breaks ties, and this
        // index is the order every read uses.
        builder
            .HasIndex(t => new { t.IsActive, t.SortOrder, t.NormalizedName })
            .HasDatabaseName("ix_asset_types_active_order");
    }
}
