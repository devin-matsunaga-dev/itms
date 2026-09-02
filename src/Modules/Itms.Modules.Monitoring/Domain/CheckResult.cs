using Itms.Platform.Text;

namespace Itms.Modules.Monitoring.Domain;

/// <summary>
/// One check of one device: what the poller found, and when.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the only high-volume table in the system</b>, and ARCHITECTURE.md §4 sets its
/// terms: raw results are kept for <see cref="RetentionDays"/> days, rolled up to hourly
/// availability and latency aggregates beyond that, and reached through a BRIN index on
/// <c>(device_id, checked_at)</c>. Plain PostgreSQL, no TimescaleDB — one less thing to
/// operate, revisited only if measured.
/// </para>
/// <para>
/// <b>Nothing in WP-3.1 writes one.</b> The table, its index, and this entity's invariants
/// exist so that <c>WP-3.3</c>'s ingestion endpoint has somewhere correct to put what
/// <c>WP-3.2</c>'s poller reports, and so that <c>WP-3.4</c>'s rollup job has a shape to
/// aggregate. Deletes are hard here rather than soft (§4), and the sweep that performs them
/// is <c>WP-3.4</c>'s hosted service — the retention is declared on this type so both
/// packages read one number rather than each choosing one.
/// </para>
/// <para>
/// <b><see cref="CheckedAt"/> and <c>created_at</c> are different facts and both are
/// kept.</b> The first is when the check ran, by the poller's clock; the second is when the
/// host stored it. They diverge whenever a batch is delivered late or a poller's clock has
/// drifted, and telling an outage from a delivery delay needs both. Every query about
/// availability reads <see cref="CheckedAt"/>.
/// </para>
/// <para>
/// <b>A result is immutable once recorded.</b> There is no method that changes one — a
/// measurement that was wrong is not corrected, it is superseded by the next one. That is
/// not the audit table's guarantee, which is enforced by a database trigger and an
/// architecture test; it is a write-once entity, at the level a measurement warrants.
/// </para>
/// </remarks>
public sealed class CheckResult
{
    /// <summary>
    /// How many days of raw results are kept before the rollups take over
    /// (ARCHITECTURE.md §4).
    /// </summary>
    /// <remarks>
    /// Declared here rather than in the job that enforces it, so <c>WP-3.4</c>'s rollup and
    /// any later report agree about where "raw" stops without either having to name a
    /// number of its own. Nothing sweeps yet — see <c>MonitoringDbContext</c>.
    /// </remarks>
    public const int RetentionDays = 30;

    /// <summary>The longest a recorded failure reason may be.</summary>
    /// <remarks>
    /// Short on purpose. This is the poller's one-line account of why a check failed —
    /// "timed out", "host unreachable", "name not resolved" — not a stack trace, and a
    /// column that invited one would be several hundred bytes on the largest table in the
    /// system for text nobody reads twice.
    /// </remarks>
    public const int FailureReasonMaxLength = 256;

    private CheckResult()
    {
        // EF Core materialisation.
    }

    /// <summary>The result's id.</summary>
    public Guid Id { get; private set; }

    /// <summary>The device that was checked.</summary>
    public Guid DeviceId { get; private set; }

    /// <summary>When the check ran, by the poller's clock (UTC).</summary>
    public DateTimeOffset CheckedAt { get; private set; }

    /// <summary>Whether the device answered.</summary>
    public bool IsSuccess { get; private set; }

    /// <summary>
    /// The round-trip time in milliseconds when the device answered, and
    /// <see langword="null"/> when it did not.
    /// </summary>
    /// <remarks>
    /// Null on a failure rather than zero, because zero is a latency and "no answer" is
    /// not one. An availability query that averaged zeros would report a suspiciously fast
    /// network for a device that is down.
    /// </remarks>
    public int? LatencyMs { get; private set; }

    /// <summary>The poller's one-line account of a failure, or <see langword="null"/> on a success.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>When the host stored the row (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Who stored it, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Null in practice and for the foreseeable future: results arrive from the poller's
    /// service credential rather than from a person, and ARCHITECTURE.md §7 keeps that
    /// credential out of the user table entirely. The column is here because §4 requires
    /// the quartet on every table, and because a result ever written by a human — a
    /// backfill, an import — should be distinguishable from one the machine reported.
    /// </remarks>
    public Guid? CreatedBy { get; private set; }

    /// <summary>When the row was last changed (UTC). Equal to <see cref="CreatedAt"/>, always.</summary>
    /// <remarks>
    /// Required by §4 and structurally constant here, because nothing updates a check
    /// result. Kept rather than argued away: a table that quietly opts out of the
    /// convention is one a later reader has to check for, and the storage is the price of
    /// not having to.
    /// </remarks>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Who last changed it. Equal to <see cref="CreatedBy"/>, always.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>Records a check that the device answered.</summary>
    /// <param name="deviceId">The device that was checked.</param>
    /// <param name="checkedAt">When the check ran, by the poller's clock (UTC).</param>
    /// <param name="latencyMs">The round-trip time in milliseconds.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who stored it, or <see langword="null"/> for the poller.</param>
    /// <returns>The new result, not yet persisted.</returns>
    public static CheckResult Success(
        Guid deviceId,
        DateTimeOffset checkedAt,
        int latencyMs,
        DateTimeOffset now,
        Guid? actor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(latencyMs);

        return New(deviceId, checkedAt, isSuccess: true, latencyMs, failureReason: null, now, actor);
    }

    /// <summary>Records a check the device did not answer.</summary>
    /// <param name="deviceId">The device that was checked.</param>
    /// <param name="checkedAt">When the check ran, by the poller's clock (UTC).</param>
    /// <param name="failureReason">The poller's one-line account, or <see langword="null"/>.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who stored it, or <see langword="null"/> for the poller.</param>
    /// <returns>The new result, not yet persisted.</returns>
    public static CheckResult Failure(
        Guid deviceId,
        DateTimeOffset checkedAt,
        string? failureReason,
        DateTimeOffset now,
        Guid? actor) =>
        New(
            deviceId,
            checkedAt,
            isSuccess: false,
            latencyMs: null,
            ReferenceText.Optional(failureReason, FailureReasonMaxLength, nameof(failureReason)),
            now,
            actor);

    /// <summary>
    /// The two factories above are the only way in, which is what makes the shape
    /// coherent: a success always carries a latency and never a reason, a failure the
    /// other way round.
    /// </summary>
    private static CheckResult New(
        Guid deviceId,
        DateTimeOffset checkedAt,
        bool isSuccess,
        int? latencyMs,
        string? failureReason,
        DateTimeOffset now,
        Guid? actor)
    {
        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException("A check result belongs to a device.", nameof(deviceId));
        }

        return new CheckResult
        {
            // v7 so the primary key is time-ordered. That matters more here than anywhere
            // else in the system: this is the table that grows by millions of rows, and a
            // random key would fragment its index on every insert.
            Id = Guid.CreateVersion7(),
            DeviceId = deviceId,
            CheckedAt = checkedAt,
            IsSuccess = isSuccess,
            LatencyMs = latencyMs,
            FailureReason = failureReason,
            CreatedAt = now,
            CreatedBy = actor,
            UpdatedAt = now,
            UpdatedBy = actor,
        };
    }
}
