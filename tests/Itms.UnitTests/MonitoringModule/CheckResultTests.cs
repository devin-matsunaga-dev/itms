using Itms.Modules.Monitoring.Domain;

namespace Itms.UnitTests.MonitoringModule;

/// <summary>
/// The check-result entity's shape rules. Nothing in WP-3.1 writes one — <c>WP-3.3</c>'s
/// ingestion does — so these assert the invariants that package will be building on.
/// </summary>
public sealed class CheckResultTests
{
    private static readonly DateTimeOffset CheckedAt = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Stored = new(2026, 9, 2, 9, 0, 2, TimeSpan.Zero);
    private static readonly Guid ADevice = Guid.CreateVersion7();

    /// <summary>
    /// The two factories are the only way in, which is what makes the shape coherent: a
    /// success carries a latency and no reason.
    /// </summary>
    [Fact]
    public void A_success_carries_a_latency_and_no_reason()
    {
        var result = CheckResult.Success(ADevice, CheckedAt, latencyMs: 12, Stored, actor: null);

        result.IsSuccess.ShouldBeTrue();
        result.LatencyMs.ShouldBe(12);
        result.FailureReason.ShouldBeNull();
    }

    /// <summary>
    /// Null rather than zero, because zero is a latency and "no answer" is not one — an
    /// average over zeros would report a suspiciously fast network for a device that is
    /// down.
    /// </summary>
    [Fact]
    public void A_failure_carries_a_reason_and_no_latency()
    {
        var result = CheckResult.Failure(ADevice, CheckedAt, "timed out", Stored, actor: null);

        result.IsSuccess.ShouldBeFalse();
        result.LatencyMs.ShouldBeNull();
        result.FailureReason.ShouldBe("timed out");
    }

    [Fact]
    public void A_failure_may_say_nothing()
    {
        var result = CheckResult.Failure(ADevice, CheckedAt, failureReason: "  ", Stored, actor: null);

        result.FailureReason.ShouldBeNull();
    }

    [Fact]
    public void A_negative_latency_is_refused()
    {
        var record = () => CheckResult.Success(ADevice, CheckedAt, latencyMs: -1, Stored, actor: null);

        record.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_result_belongs_to_a_device()
    {
        var record = () => CheckResult.Success(Guid.Empty, CheckedAt, latencyMs: 1, Stored, actor: null);

        record.ShouldThrow<ArgumentException>();
    }

    /// <summary>
    /// When the check ran and when the host stored it are different facts and both are
    /// kept: they diverge whenever a batch is delivered late or a poller's clock has
    /// drifted, and telling an outage from a delivery delay needs both.
    /// </summary>
    [Fact]
    public void The_check_instant_and_the_storage_instant_are_recorded_separately()
    {
        var result = CheckResult.Success(ADevice, CheckedAt, latencyMs: 4, Stored, actor: null);

        result.CheckedAt.ShouldBe(CheckedAt);
        result.CreatedAt.ShouldBe(Stored);
        result.UpdatedAt.ShouldBe(Stored);
    }

    /// <summary>
    /// ARCHITECTURE.md §4's retention is declared on the entity so <c>WP-3.4</c>'s rollup
    /// and any later report read one number rather than each choosing one.
    /// </summary>
    [Fact]
    public void The_raw_retention_is_thirty_days()
    {
        CheckResult.RetentionDays.ShouldBe(30);
    }
}
