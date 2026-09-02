using Itms.Modules.Monitoring.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itms.Modules.Monitoring.Persistence.Configurations;

/// <summary>Maps <see cref="CheckResult"/> to <c>monitoring.check_results</c>.</summary>
/// <remarks>
/// This is the table ARCHITECTURE.md §4 singles out as the only high-volume one in the
/// system, and everything unusual about this configuration follows from that.
/// </remarks>
internal sealed class CheckResultConfiguration : IEntityTypeConfiguration<CheckResult>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CheckResult> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("check_results");
        builder.HasKey(result => result.Id).HasName("pk_check_results");

        builder.Property(result => result.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(result => result.DeviceId).HasColumnName("device_id").IsRequired();
        builder.Property(result => result.CheckedAt).HasColumnName("checked_at").IsRequired();
        builder.Property(result => result.IsSuccess).HasColumnName("is_success").IsRequired();
        builder.Property(result => result.LatencyMs).HasColumnName("latency_ms");
        builder
            .Property(result => result.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(CheckResult.FailureReasonMaxLength);

        builder.Property(result => result.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(result => result.CreatedBy).HasColumnName("created_by");
        builder.Property(result => result.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(result => result.UpdatedBy).HasColumnName("updated_by");

        // Both tables are this module's, in one schema, so a real foreign key is legal
        // (§3 rule 6 forbids one only across a module boundary). RESTRICT rather than
        // CASCADE, following fk_ticket_history_ticket_id: no delete path for a device
        // exists yet, and when one is written it should have to say out loud what happens
        // to the measurements — better a refused delete than silently discarded evidence
        // of an outage.
        builder
            .HasOne<MonitoredDevice>()
            .WithMany()
            .HasForeignKey(result => result.DeviceId)
            .HasConstraintName("fk_check_results_device_id")
            .OnDelete(DeleteBehavior.Restrict);

        // ARCHITECTURE.md §4 names this index specifically: "a BRIN index on
        // (device_id, checked_at) is sufficient at this scale".
        //
        // BRIN indexes the physical order of the heap rather than the values, which is what
        // makes it a few pages where a B-tree would be gigabytes — and it works here
        // because results arrive in checked_at order and are therefore stored in it. The
        // correlation is on checked_at, not on device_id, so a query for one device over a
        // wide window still scans the ranges that window covers; that is the trade §4 chose
        // knowingly, and WP-3.4's hourly rollups are what keep the wide-window questions
        // off this table entirely. WP-6.4 owns the measured review.
        builder
            .HasIndex(result => new { result.DeviceId, result.CheckedAt })
            .HasMethod("brin")
            .HasDatabaseName("ix_check_results_device_id_checked_at");
    }
}
