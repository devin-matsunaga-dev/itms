using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Identity.Persistence.Configurations;

/// <summary>Maps per-user claims to <c>identity.user_claims</c>.</summary>
internal sealed class UserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("user_claims");
        builder.HasKey(c => c.Id).HasName("pk_user_claims");

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.UserId).HasColumnName("user_id");
        builder.Property(c => c.ClaimType).HasColumnName("claim_type").HasMaxLength(256);
        builder.Property(c => c.ClaimValue).HasColumnName("claim_value").HasMaxLength(1024);

        builder
            .HasOne<Domain.ItmsUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .HasConstraintName("fk_user_claims_users")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.UserId).HasDatabaseName("ix_user_claims_user_id");
    }
}
