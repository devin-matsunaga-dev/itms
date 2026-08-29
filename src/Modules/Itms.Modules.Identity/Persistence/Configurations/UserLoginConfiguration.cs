using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Identity.Persistence.Configurations;

/// <summary>
/// Maps external logins to <c>identity.user_logins</c>.
/// </summary>
/// <remarks>
/// V1 has local accounts only (ARCHITECTURE.md §7) — no external IdP, no LDAP — so this
/// table stays empty. It is mapped rather than ignored because <c>UserStore</c> queries
/// it while deleting a user, and an unmapped table would turn that into a runtime error
/// the first time an administrator removes an account.
/// </remarks>
internal sealed class UserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("user_logins");
        builder.HasKey(l => new { l.LoginProvider, l.ProviderKey }).HasName("pk_user_logins");

        builder.Property(l => l.LoginProvider).HasColumnName("login_provider").HasMaxLength(128);
        builder.Property(l => l.ProviderKey).HasColumnName("provider_key").HasMaxLength(128);
        builder.Property(l => l.ProviderDisplayName).HasColumnName("provider_display_name").HasMaxLength(128);
        builder.Property(l => l.UserId).HasColumnName("user_id");

        builder
            .HasOne<Domain.ItmsUser>()
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .HasConstraintName("fk_user_logins_users")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.UserId).HasDatabaseName("ix_user_logins_user_id");
    }
}
