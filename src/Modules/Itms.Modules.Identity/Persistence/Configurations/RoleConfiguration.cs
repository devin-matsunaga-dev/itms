using Itms.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Identity.Persistence.Configurations;

/// <summary>Maps <see cref="ItmsRole"/> to <c>identity.roles</c>.</summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<ItmsRole>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ItmsRole> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("roles");
        builder.HasKey(r => r.Id).HasName("pk_roles");

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(64).IsRequired();
        builder.Property(r => r.NormalizedName).HasColumnName("normalized_name").HasMaxLength(64).IsRequired();
        builder.Property(r => r.ConcurrencyStamp).HasColumnName("concurrency_stamp").IsConcurrencyToken();
        builder.Property(r => r.Description).HasColumnName("description").HasMaxLength(400).IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(r => r.NormalizedName).HasDatabaseName("ux_roles_normalized_name").IsUnique();
    }
}
