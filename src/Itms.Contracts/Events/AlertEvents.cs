namespace Itms.Contracts.Events;

/// <summary>
/// An alert was raised for a device. It carries the device and location context at
/// the moment of raising, because invariant 7 says an alert keeps that context even
/// if the device is later moved or retired.
/// </summary>
/// <param name="AlertId">The alert.</param>
/// <param name="DeviceId">The device the alert is about.</param>
/// <param name="AssetId">The asset that device is.</param>
/// <param name="AssetTag">The asset tag at the time the alert was raised.</param>
/// <param name="LocationPath">The full location path at the time the alert was raised, held as text so it stays true after a location is renamed or moved.</param>
/// <param name="Severity">The alert severity.</param>
/// <param name="Summary">One line describing what happened, used in the notification and in any ticket created from the alert.</param>
public sealed record AlertRaised(
    Guid AlertId,
    Guid DeviceId,
    Guid AssetId,
    string AssetTag,
    string? LocationPath,
    string Severity,
    string Summary) : DomainEvent;

/// <summary>
/// An alert was resolved, whether by the device recovering or by a technician
/// acknowledging and closing it.
/// </summary>
/// <param name="AlertId">The alert.</param>
/// <param name="DeviceId">The device the alert was about.</param>
/// <param name="ResolvedAt">When it was resolved, in UTC.</param>
/// <param name="ResolvedAutomatically">True when the device recovered on its own; false when a person closed it.</param>
public sealed record AlertResolved(
    Guid AlertId,
    Guid DeviceId,
    DateTimeOffset ResolvedAt,
    bool ResolvedAutomatically) : DomainEvent;
