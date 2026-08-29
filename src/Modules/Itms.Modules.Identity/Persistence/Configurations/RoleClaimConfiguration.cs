using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Identity.Persistence.Configurations;

/// <summary>Maps per-role claims to <c>identity.role_claims</c>.</summary>
internal sealed class RoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("role_claims");
        builder.HasKey(c => c.Id).HasName("pk_role_claims");

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.RoleId).HasColumnName("role_id");
        builder.Property(c => c.ClaimType).HasColumnName("claim_type").HasMaxLength(256);
        builder.Property(c => c.ClaimValue).HasColumnName("claim_value").HasMaxLength(1024);

        builder
            .HasOne<Domain.ItmsRole>()
            .WithMany()
            .HasForeignKey(c => c.RoleId)
            .HasConstraintName("fk_role_claims_roles")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.RoleId).HasDatabaseName("ix_role_claims_role_id");
    }
}
