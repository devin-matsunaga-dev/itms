using Itms.Modules.Directory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Directory.Persistence.Configurations;

/// <summary>Maps <see cref="Location"/> to <c>directory.locations</c>.</summary>
internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    /// <summary>
    /// Wide enough for <see cref="LocationHierarchy.MaxDepth"/> levels of
    /// <c>/{32 hex}/</c>, with room to spare.
    /// </summary>
    private const int PathMaxLength = 512;

    /// <summary>Wide enough for <see cref="LocationHierarchy.MaxDepth"/> full-length names and their separators.</summary>
    private const int FullPathMaxLength = 1400;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("locations");
        builder.HasKey(l => l.Id).HasName("pk_locations");

        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(l => l.Name).HasColumnName("name").HasMaxLength(Location.NameMaxLength).IsRequired();
        builder.Property(l => l.NormalizedName).HasColumnName("normalized_name").HasMaxLength(Location.NameMaxLength).IsRequired();

        // Stored as text rather than an integer: a directory row is read far more often
        // in psql during an incident than an enum is renumbered, and "Room" beats "6".
        builder
            .Property(l => l.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(l => l.ParentId).HasColumnName("parent_id");
        builder.Property(l => l.Path).HasColumnName("path").HasMaxLength(PathMaxLength).IsRequired();
        builder.Property(l => l.FullPath).HasColumnName("full_path").HasMaxLength(FullPathMaxLength).IsRequired();
        builder.Property(l => l.Depth).HasColumnName("depth").IsRequired();
        builder.Property(l => l.Description).HasColumnName("description").HasMaxLength(Location.DescriptionMaxLength);
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(l => l.CreatedBy).HasColumnName("created_by");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by");

        // The tree's own edge. A foreign key inside one module's schema, which §3 rule 6
        // permits and wants. Restrict rather than Cascade: deleting a parent with
        // children is refused by the handler with a 409, and the database refusing it too
        // means no future code path can quietly delete a subtree instead.
        builder
            .HasOne<Location>()
            .WithMany()
            .HasForeignKey(l => l.ParentId)
            .HasConstraintName("fk_locations_parent")
            .OnDelete(DeleteBehavior.Restrict);

        // Listing a node's children, and the child count the delete check reads.
        builder.HasIndex(l => l.ParentId).HasDatabaseName("ix_locations_parent");

        // Two rooms called "G-04" on the same floor would make the picker a coin toss.
        // NULLs are not distinct here, so the rule also covers two roots sharing a name —
        // PostgreSQL's default would let both through.
        builder
            .HasIndex(l => new { l.ParentId, l.NormalizedName })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ux_locations_parent_name");

        // Subtree queries are prefix matches on the materialised path. The pattern
        // operator class is what lets a LIKE 'prefix%' use this index at all: the
        // database's default collation is not C, and without it PostgreSQL would fall
        // back to a sequential scan on every rename and every subtree read.
        builder
            .HasIndex(l => l.Path)
            .HasDatabaseName("ix_locations_path")
            .HasOperators("varchar_pattern_ops");

        // The default ordering of the flat tree listing.
        builder.HasIndex(l => l.FullPath).HasDatabaseName("ix_locations_full_path");
    }
}
