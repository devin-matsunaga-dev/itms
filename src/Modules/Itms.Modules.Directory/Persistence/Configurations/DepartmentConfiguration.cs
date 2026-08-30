using Itms.Modules.Directory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Directory.Persistence.Configurations;

/// <summary>Maps <see cref="Department"/> to <c>directory.departments</c>.</summary>
internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("departments");
        builder.HasKey(d => d.Id).HasName("pk_departments");

        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(Department.NameMaxLength).IsRequired();
        builder.Property(d => d.NormalizedName).HasColumnName("normalized_name").HasMaxLength(Department.NameMaxLength).IsRequired();
        builder.Property(d => d.Code).HasColumnName("code").HasMaxLength(Department.CodeMaxLength);
        builder.Property(d => d.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(Department.CodeMaxLength);
        builder.Property(d => d.Description).HasColumnName("description").HasMaxLength(Department.DescriptionMaxLength);
        builder.Property(d => d.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");

        // Two departments called "Finance" would make every ticket report ambiguous, and
        // the case-insensitivity is what stops "finance" being accepted as a second one.
        builder
            .HasIndex(d => d.NormalizedName)
            .IsUnique()
            .HasDatabaseName("ux_departments_normalized_name");

        // Unique only where present: PostgreSQL treats NULLs as distinct in a unique
        // index, so any number of departments may have no code at all.
        builder
            .HasIndex(d => d.NormalizedCode)
            .IsUnique()
            .HasDatabaseName("ux_departments_normalized_code");

        // The list screen's default view is the active departments in name order.
        builder
            .HasIndex(d => new { d.IsActive, d.NormalizedName })
            .HasDatabaseName("ix_departments_active_name");
    }
}
