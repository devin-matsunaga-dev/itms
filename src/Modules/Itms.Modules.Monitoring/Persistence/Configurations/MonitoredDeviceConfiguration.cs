using Itms.Modules.Monitoring.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Monitoring.Persistence.Configurations;

/// <summary>Maps <see cref="MonitoredDevice"/> to <c>monitoring.devices</c>.</summary>
internal sealed class MonitoredDeviceConfiguration : IEntityTypeConfiguration<MonitoredDevice>
{
    /// <summary>
    /// The name of the shadow property carrying PostgreSQL's <c>xmin</c> row version.
    /// </summary>
    /// <remarks>
    /// Named here rather than spelled at each use, because the handlers read it back
    /// through <c>EF.Property&lt;uint&gt;</c> and a mistyped string would be a runtime
    /// failure rather than a compile error. This is the same shape <c>TicketConfiguration</c>
    /// and <c>AssetConfiguration</c> use.
    /// </remarks>
    public const string VersionProperty = "Version";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MonitoredDevice> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("devices");
        builder.HasKey(device => device.Id).HasName("pk_devices");

        builder.Property(device => device.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(device => device.AssetId).HasColumnName("asset_id").IsRequired();
        builder
            .Property(device => device.AssetTag)
            .HasColumnName("asset_tag")
            .HasMaxLength(MonitoredDevice.AssetTagMaxLength)
            .IsRequired();

        builder
            .Property(device => device.Hostname)
            .HasColumnName("hostname")
            .HasMaxLength(MonitoredDevice.HostnameMaxLength);

        builder
            .Property(device => device.NormalizedHostname)
            .HasColumnName("normalized_hostname")
            .HasMaxLength(MonitoredDevice.HostnameMaxLength);

        // inet, not text: PostgreSQL then refuses a value that is not an address, and two
        // spellings of one address cannot be recorded as two devices. Npgsql maps
        // System.Net.IPAddress onto it natively.
        builder.Property(device => device.IpAddress).HasColumnName("ip_address").HasColumnType("inet");

        builder.Property(device => device.MonitoringEnabled).HasColumnName("monitoring_enabled").IsRequired();
        builder.Property(device => device.PollIntervalSeconds).HasColumnName("poll_interval_seconds").IsRequired();
        builder.Property(device => device.FailureThreshold).HasColumnName("failure_threshold").IsRequired();
        builder.Property(device => device.SnmpEnabled).HasColumnName("snmp_enabled").IsRequired();
        builder.Property(device => device.SnmpPort).HasColumnName("snmp_port").IsRequired();

        // The one secret in the module. It is a column because it varies per device, and
        // it is write-only everywhere above this line — see MonitoredDevice.SnmpCommunity
        // for the four rules that keeps, and WP-6.3 for encryption at rest.
        builder
            .Property(device => device.SnmpCommunity)
            .HasColumnName("snmp_community")
            .HasMaxLength(SnmpSettings.CommunityMaxLength);

        builder.Property(device => device.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(device => device.CreatedBy).HasColumnName("created_by");
        builder.Property(device => device.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(device => device.UpdatedBy).HasColumnName("updated_by");

        // Computed from SnmpCommunity and never stored: a column would be a second place
        // for the same fact to be wrong.
        builder.Ignore(device => device.HasSnmpCredential);

        // ARCHITECTURE.md §6 wants optimistic concurrency where two people can be looking
        // at the same record, and a device's settings screen is such a place. xmin is
        // PostgreSQL's own row version, so this costs no column and no write.
        //
        // Mapped by hand because Npgsql 10 no longer ships UseXminAsConcurrencyToken().
        // NOTE FOR THE MIGRATION: the provider also no longer suppresses the column when
        // scaffolding, so the generated `xmin = table.Column<uint>(...)` line must be
        // deleted from the migration before it is ever applied — PostgreSQL refuses
        // CREATE TABLE with a user column called xmin. WP-1.2 hit this first and both its
        // migration and WP-2.1's carry the same note.
        builder
            .Property<uint>(VersionProperty)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Invariant 6's other half. Not a foreign key — §3 rule 6 forbids one across a
        // module boundary, and the asset lives in the assets schema — but unique, so one
        // machine cannot acquire two monitoring states, two outage histories and two of
        // every alert that follows from them. The handler checks first and returns a 409
        // naming the tag; this is what makes the rare concurrent case safe.
        builder
            .HasIndex(device => device.AssetId)
            .IsUnique()
            .HasDatabaseName("ux_devices_asset_id");

        // For the register's search. Deliberately not unique — see
        // MonitoredDevice.NormalizedHostname on why one name can legitimately belong to two
        // devices.
        builder
            .HasIndex(device => device.NormalizedHostname)
            .HasDatabaseName("ix_devices_normalized_hostname");

        // The poller's own question, and WP-3.2's first query: which devices am I to check?
        // Leading with the flag rather than with the interval, because it is the selective
        // half — a disabled device is skipped entirely, and the interval only orders what
        // is left.
        builder
            .HasIndex(device => new { device.MonitoringEnabled, device.PollIntervalSeconds })
            .HasDatabaseName("ix_devices_monitoring_enabled_poll_interval");
    }
}
