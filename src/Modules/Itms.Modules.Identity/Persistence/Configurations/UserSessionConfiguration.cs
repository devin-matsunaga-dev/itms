using Itms.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Identity.Persistence.Configurations;

/// <summary>Maps <see cref="UserSession"/> to <c>identity.sessions</c>.</summary>
internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("sessions");
        builder.HasKey(s => s.Id).HasName("pk_sessions");

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(s => s.IssuedAt).HasColumnName("issued_at").IsRequired();
        builder.Property(s => s.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(s => s.RevokedAt).HasColumnName("revoked_at");
        builder.Property(s => s.RevokedReason).HasColumnName("revoked_reason").HasMaxLength(64);
        builder.Property(s => s.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
        builder.Property(s => s.UserAgent).HasColumnName("user_agent").HasMaxLength(512);

        // Sessions belong to the user and die with them; this is a foreign key inside a
        // single module's schema, which §3 rule 6 permits and in fact wants.
        builder
            .HasOne<ItmsUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .HasConstraintName("fk_sessions_users")
            .OnDelete(DeleteBehavior.Cascade);

        // "Revoke every other session of this user" is the one write path that does not
        // go through the primary key, and it runs on every password change.
        builder.HasIndex(s => new { s.UserId, s.RevokedAt }).HasDatabaseName("ix_sessions_user_revoked");

        // Expired sessions are hard-deleted (ARCHITECTURE.md §4), by a sweep that finds
        // them through this index.
        builder.HasIndex(s => s.ExpiresAt).HasDatabaseName("ix_sessions_expires_at");
    }
}
