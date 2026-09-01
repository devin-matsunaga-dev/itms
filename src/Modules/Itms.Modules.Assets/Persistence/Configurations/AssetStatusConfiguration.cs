using Itms.Modules.Assets.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Assets.Persistence.Configurations;

/// <summary>Maps <see cref="AssetStatus"/> to <c>assets.asset_statuses</c>.</summary>
internal sealed class AssetStatusConfiguration : IEntityTypeConfiguration<AssetStatus>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AssetStatus> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("asset_statuses");
        builder.HasKey(s => s.Id).HasName("pk_asset_statuses");

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.Code).HasColumnName("code").HasMaxLength(AssetStatusCode.MaxLength).IsRequired();
        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(AssetStatus.NameMaxLength).IsRequired();
        builder.Property(s => s.NormalizedName).HasColumnName("normalized_name").HasMaxLength(AssetStatus.NameMaxLength).IsRequired();
        builder.Property(s => s.Description).HasColumnName("description").HasMaxLength(AssetStatus.DescriptionMaxLength);
        builder.Property(s => s.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(s => s.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(s => s.NormalizedName)
            .IsUnique()
            .HasDatabaseName("ux_asset_statuses_normalized_name");

        // The code is what WP-2.2's lifecycle methods and DESIGN.md's colours key off, so
        // two rows claiming one would make both ambiguous.
        builder
            .HasIndex(s => s.Code)
            .IsUnique()
            .HasDatabaseName("ux_asset_statuses_code");

        builder
            .HasIndex(s => new { s.IsActive, s.SortOrder, s.NormalizedName })
            .HasDatabaseName("ix_asset_statuses_active_order");
    }
}
