using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Identity.Persistence.Configurations;

/// <summary>
/// Maps per-user tokens to <c>identity.user_tokens</c>. Nothing in V1 writes it —
/// password-reset tokens are protected payloads, not stored rows — but the token
/// providers <c>UserManager</c> resolves expect the store to exist.
/// </summary>
internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("user_tokens");
        builder.HasKey(t => new { t.UserId, t.LoginProvider, t.Name }).HasName("pk_user_tokens");

        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.Property(t => t.LoginProvider).HasColumnName("login_provider").HasMaxLength(128);
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(128);
        builder.Property(t => t.Value).HasColumnName("value");

        builder
            .HasOne<Domain.ItmsUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .HasConstraintName("fk_user_tokens_users")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
