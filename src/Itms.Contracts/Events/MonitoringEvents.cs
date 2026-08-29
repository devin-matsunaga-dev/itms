namespace Itms.Contracts.Events;

/// <summary>
/// A monitored device crossed the failure threshold and is considered offline.
/// Raised by Monitoring, never by the poller: the poller reports raw check results
/// and state-transition logic lives in one place (ARCHITECTURE.md §5).
/// </summary>
/// <param name="DeviceId">The monitored device.</param>
/// <param name="AssetId">The asset the device is (invariant 6 — a device is always an asset).</param>
/// <param name="AssetTag">The asset tag, for alert and notification text.</param>
/// <param name="LocationId">Where it is, captured now because an alert must carry its location context at the time it was raised (invariant 7).</param>
/// <param name="LastSeenAt">The last successful check, in UTC.</param>
/// <param name="ConsecutiveFailures">How many checks failed before the device was declared offline.</param>
public sealed record DeviceWentOffline(
    Guid DeviceId,
    Guid AssetId,
    string AssetTag,
    Guid? LocationId,
    DateTimeOffset? LastSeenAt,
    int ConsecutiveFailures) : DomainEvent;

/// <summary>
/// A device that was offline answered a check. One success restores it
/// (ARCHITECTURE.md §9), which is what resolves the open alert.
/// </summary>
/// <param name="DeviceId">The monitored device.</param>
/// <param name="AssetId">The asset the device is.</param>
/// <param name="AssetTag">The asset tag.</param>
/// <param name="OfflineSince">When it went offline, in UTC, so a consumer can report the outage duration.</param>
public sealed record DeviceRecovered(
    Guid DeviceId,
    Guid AssetId,
    string AssetTag,
    DateTimeOffset OfflineSince) : DomainEvent;
