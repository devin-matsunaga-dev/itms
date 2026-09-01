using Itms.Modules.Assets.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Assets.Persistence.Configurations;

/// <summary>Maps <see cref="Asset"/> to <c>assets.assets</c>.</summary>
internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    /// <summary>
    /// The name of the shadow property carrying PostgreSQL's <c>xmin</c> row version.
    /// </summary>
    /// <remarks>
    /// Named here rather than spelled at each use, because a package turning this into an
    /// ETag reads it back through <c>EF.Property&lt;uint&gt;</c> and a mistyped string would
    /// be a runtime failure rather than a compile error. This is the same shape
    /// <c>TicketConfiguration</c> uses.
    /// </remarks>
    public const string VersionProperty = "Version";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("assets");
        builder.HasKey(a => a.Id).HasName("pk_assets");

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.AssetTag).HasColumnName("asset_tag").HasMaxLength(AssetTagRules.MaxLength).IsRequired();
        builder.Property(a => a.NormalizedAssetTag).HasColumnName("normalized_asset_tag").HasMaxLength(AssetTagRules.MaxLength).IsRequired();
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(Asset.NameMaxLength);
        builder.Property(a => a.SerialNumber).HasColumnName("serial_number").HasMaxLength(Asset.SerialNumberMaxLength);
        builder.Property(a => a.NormalizedSerialNumber).HasColumnName("normalized_serial_number").HasMaxLength(Asset.SerialNumberMaxLength);
        builder.Property(a => a.Barcode).HasColumnName("barcode").HasMaxLength(Asset.BarcodeMaxLength);
        builder.Property(a => a.Manufacturer).HasColumnName("manufacturer").HasMaxLength(Asset.ManufacturerMaxLength);
        builder.Property(a => a.NormalizedManufacturer).HasColumnName("normalized_manufacturer").HasMaxLength(Asset.ManufacturerMaxLength);
        builder.Property(a => a.Model).HasColumnName("model").HasMaxLength(Asset.ModelMaxLength);
        builder.Property(a => a.AssetTypeId).HasColumnName("asset_type_id").IsRequired();
        builder.Property(a => a.AssetStatusId).HasColumnName("asset_status_id").IsRequired();
        builder.Property(a => a.AssignedToUserId).HasColumnName("assigned_to_user_id");
        builder.Property(a => a.AssignedToUserName).HasColumnName("assigned_to_user_name").HasMaxLength(Asset.AssignedToUserNameMaxLength);
        builder.Property(a => a.DepartmentId).HasColumnName("department_id");
        builder.Property(a => a.DepartmentName).HasColumnName("department_name").HasMaxLength(Asset.DepartmentNameMaxLength);
        builder.Property(a => a.LocationId).HasColumnName("location_id");
        builder.Property(a => a.LocationPath).HasColumnName("location_path").HasMaxLength(Asset.LocationPathMaxLength);
        builder.Property(a => a.PurchaseDate).HasColumnName("purchase_date");
        builder.Property(a => a.WarrantyExpiresAt).HasColumnName("warranty_expires_at");
        builder.Property(a => a.Vendor).HasColumnName("vendor").HasMaxLength(Asset.VendorMaxLength);

        // numeric(12,2), not a floating-point type: money that is added up must not drift.
        // There is no currency column — see Asset.Cost for the single-currency assumption
        // that decision rests on.
        builder.Property(a => a.Cost).HasColumnName("cost").HasPrecision(12, 2);

        builder.Property(a => a.Notes).HasColumnName("notes").HasMaxLength(Asset.NotesMaxLength);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at");

        // ARCHITECTURE.md §6 wants optimistic concurrency on tickets *and assets*. xmin is
        // PostgreSQL's own row version, so this costs no column and no write.
        //
        // Mapped by hand because Npgsql 10 no longer ships UseXminAsConcurrencyToken().
        // NOTE FOR THE MIGRATION: the provider also no longer suppresses the column when
        // scaffolding, so the generated `xmin = table.Column<uint>(...)` line must be
        // deleted from the migration before it is ever applied — PostgreSQL refuses
        // CREATE TABLE with a user column called xmin. WP-1.2 hit this first and its
        // migration carries the same note.
        builder
            .Property<uint>(VersionProperty)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Inert today — nothing soft-deletes an asset yet, and no work package names a
        // delete path — and that is precisely why it goes in now. Added later, it would
        // silently change the meaning of every list query written in the meantime. A screen
        // that genuinely wants deleted rows asks with IgnoreQueryFilters().
        builder.HasQueryFilter(a => a.DeletedAt == null);

        // Invariant 4's uniqueness half, and it is deliberately NOT filtered on
        // deleted_at: a soft-deleted asset keeps its tag reserved forever. Reusing the tag
        // of a machine that was written off would make every historical ticket, alert, and
        // audit row that names it ambiguous, which is the opposite of what an immutable
        // identifier is for.
        builder
            .HasIndex(a => a.NormalizedAssetTag)
            .IsUnique()
            .HasDatabaseName("ux_assets_normalized_asset_tag");

        // "Serial numbers are unique per manufacturer where present" (invariant 4). Two
        // vendors numbering their products from 1 is ordinary, so the pair is what is
        // unique — and the partial filter is the "where present": an asset with no serial,
        // or a serial and no manufacturer, collides with nothing.
        builder
            .HasIndex(a => new { a.NormalizedManufacturer, a.NormalizedSerialNumber })
            .IsUnique()
            .HasFilter(@"""normalized_manufacturer"" IS NOT NULL AND ""normalized_serial_number"" IS NOT NULL")
            .HasDatabaseName("ux_assets_manufacturer_serial_number");

        // Real foreign keys with no navigation property on either side. §3 rule 6 forbids
        // one only across a module boundary; these tables are all this module's, in one
        // schema, which is what makes renaming a type reach every asset for free.
        // RESTRICT is what makes "a reference type in use cannot be removed" structural
        // rather than a matter of no route being mapped — the same pairing WP-1.1 and
        // WP-1.2 used for a ticket's category and priority.
        builder
            .HasOne<AssetType>()
            .WithMany()
            .HasForeignKey(a => a.AssetTypeId)
            .HasConstraintName("fk_assets_asset_type_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<AssetStatus>()
            .WithMany()
            .HasForeignKey(a => a.AssetStatusId)
            .HasConstraintName("fk_assets_asset_status_id")
            .OnDelete(DeleteBehavior.Restrict);

        // The shapes WP-2.3 filters on. Deliberately a first guess and not a measured set,
        // exactly as WP-1.2's seven were: WP-6.4 owns the review against the real query
        // set, and nothing here is indexed for a filter nobody has written yet.
        builder
            .HasIndex(a => new { a.AssetStatusId, a.AssetTypeId })
            .HasDatabaseName("ix_assets_status_type");

        builder
            .HasIndex(a => a.AssignedToUserId)
            .HasDatabaseName("ix_assets_assigned_to_user_id");

        builder
            .HasIndex(a => a.LocationId)
            .HasDatabaseName("ix_assets_location_id");

        builder
            .HasIndex(a => a.DepartmentId)
            .HasDatabaseName("ix_assets_department_id");

        // WP-2.3's "warranty expiring within N days", which WP-5.2's dashboard tile reads.
        builder
            .HasIndex(a => a.WarrantyExpiresAt)
            .HasDatabaseName("ix_assets_warranty_expires_at");
    }
}
