using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Identity.Persistence.Configurations;

/// <summary>
/// Maps role membership to <c>identity.user_roles</c>.
/// </summary>
/// <remarks>
/// The framework-owned join and claim tables carry no <c>created_at</c>/<c>created_by</c>
/// columns, unlike every table this system designs itself: <c>UserManager</c> writes them
/// and has nowhere to put an actor. Who changed someone's role is recorded where
/// ARCHITECTURE.md §8 says it belongs — in the append-only audit log (WP-0.7).
/// </remarks>
internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<Guid>>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("user_roles");
        builder.HasKey(r => new { r.UserId, r.RoleId }).HasName("pk_user_roles");

        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.Property(r => r.RoleId).HasColumnName("role_id");

        builder
            .HasOne<Domain.ItmsUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .HasConstraintName("fk_user_roles_users")
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Domain.ItmsRole>()
            .WithMany()
            .HasForeignKey(r => r.RoleId)
            .HasConstraintName("fk_user_roles_roles")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.RoleId).HasDatabaseName("ix_user_roles_role_id");
    }
}
