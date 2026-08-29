using Itms.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Identity.Persistence.Configurations;

/// <summary>Maps <see cref="ItmsUser"/> to <c>identity.users</c>.</summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<ItmsUser>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ItmsUser> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("users");
        builder.HasKey(u => u.Id).HasName("pk_users");

        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(u => u.UserName).HasColumnName("user_name").HasMaxLength(128).IsRequired();
        builder.Property(u => u.NormalizedUserName).HasColumnName("normalized_user_name").HasMaxLength(128).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
        builder.Property(u => u.EmailConfirmed).HasColumnName("email_confirmed");
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash");
        builder.Property(u => u.SecurityStamp).HasColumnName("security_stamp");
        builder.Property(u => u.ConcurrencyStamp).HasColumnName("concurrency_stamp").IsConcurrencyToken();
        builder.Property(u => u.PhoneNumber).HasColumnName("phone_number").HasMaxLength(32);
        builder.Property(u => u.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
        builder.Property(u => u.TwoFactorEnabled).HasColumnName("two_factor_enabled");
        builder.Property(u => u.LockoutEnd).HasColumnName("lockout_end");
        builder.Property(u => u.LockoutEnabled).HasColumnName("lockout_enabled");
        builder.Property(u => u.AccessFailedCount).HasColumnName("access_failed_count");

        builder.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(u => u.DepartmentId).HasColumnName("department_id");
        builder.Property(u => u.LocationId).HasColumnName("location_id");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.CreatedBy).HasColumnName("created_by");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ux_users_normalized_user_name").IsUnique();
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ux_users_normalized_email").IsUnique();

        // Requester and assignee pickers list active users; the flag is low-cardinality,
        // so it earns an index only as the leading column of the name it is filtered with.
        builder.HasIndex(u => new { u.IsActive, u.DisplayName }).HasDatabaseName("ix_users_active_display_name");
    }
}
