using Itms.Modules.Assets.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Assets.Persistence.Configurations;

/// <summary>Maps <see cref="AssetHistoryEntry"/> to <c>assets.asset_history</c>.</summary>
internal sealed class AssetHistoryEntryConfiguration : IEntityTypeConfiguration<AssetHistoryEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AssetHistoryEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("asset_history");
        builder.HasKey(entry => entry.Id).HasName("pk_asset_history");

        builder.Property(entry => entry.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entry => entry.AssetId).HasColumnName("asset_id").IsRequired();

        // Text, for the reason AssetChangeKind documents: this column is read at a psql
        // prompt when somebody is chasing a missing laptop.
        builder
            .Property(entry => entry.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder
            .Property(entry => entry.FromValue)
            .HasColumnName("from_value")
            .HasMaxLength(AssetHistoryEntry.ValueMaxLength);

        builder
            .Property(entry => entry.ToValue)
            .HasColumnName("to_value")
            .HasMaxLength(AssetHistoryEntry.ValueMaxLength);

        builder
            .Property(entry => entry.Note)
            .HasColumnName("note")
            .HasMaxLength(AssetHistoryEntry.NoteMaxLength);

        builder.Property(entry => entry.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(entry => entry.Sequence).HasColumnName("sequence").IsRequired();
        builder.Property(entry => entry.ActorId).HasColumnName("actor_id");

        builder
            .Property(entry => entry.ActorName)
            .HasColumnName("actor_name")
            .HasMaxLength(AssetHistoryEntry.ActorNameMaxLength);

        // A real foreign key with no navigation property, exactly as AssetConfiguration
        // maps the type and status ones: §3 rule 6 forbids one only across a module
        // boundary, and CONVENTIONS.md's ban on lazy loading means a navigation would only
        // ever be a way to load an aggregate to render a list.
        //
        // RESTRICT rather than CASCADE, because an asset delete is soft (ARCHITECTURE.md
        // §4) and no code path hard-deletes one. If the package that finally owns the
        // delete path adds one, it has to decide what happens to the timeline deliberately
        // rather than discover that the database silently threw it away.
        builder
            .HasOne<Asset>()
            .WithMany()
            .HasForeignKey(entry => entry.AssetId)
            .HasConstraintName("fk_asset_history_asset_id")
            .OnDelete(DeleteBehavior.Restrict);

        // The only query this table serves: one asset's timeline, newest first. Descending
        // on the instant and the ordinal so the index is read forwards rather than
        // backwards, in exactly the order ListAssetHistoryHandler asks for.
        builder
            .HasIndex(entry => new { entry.AssetId, entry.OccurredAt, entry.Sequence })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_asset_history_asset_id_occurred_at");
    }
}
