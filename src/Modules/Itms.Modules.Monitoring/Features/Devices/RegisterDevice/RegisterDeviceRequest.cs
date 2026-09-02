namespace Itms.Modules.Monitoring.Features.Devices.RegisterDevice;

/// <summary>
/// The fields a monitored device is registered from.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="AssetId"/> is required and is the only way in.</b> Invariant 6 says a
/// monitored device is always an asset and monitoring cannot create device records of its
/// own — so there is no field here for a name, a type, or anything else that would let a
/// caller describe a device this system does not already own as equipment. Register the
/// asset first.
/// </para>
/// <para>
/// <b><see cref="SnmpCommunity"/> is the one write-only field in the API.</b> It may be
/// supplied here so that a device with SNMP enabled can be made usable in one call, and it
/// is never returned by any read afterwards — the response answers
/// <c>snmpCredentialSet</c> instead. Changing it later is
/// <c>PUT /api/v1/devices/{id}/snmp-credential</c>; the ordinary edit cannot reach it.
/// </para>
/// </remarks>
/// <param name="AssetId">The asset this device is. Required, and must already exist.</param>
/// <param name="Hostname">
/// The name the device answers to. Optional, but a device needs this or an address.
/// </param>
/// <param name="IpAddress">
/// The address it is polled at, as text. Optional, but a device needs this or a hostname.
/// </param>
/// <param name="MonitoringEnabled">
/// Whether the poller should pick it up. Optional; defaults to true, because registering a
/// device is asking for it to be watched.
/// </param>
/// <param name="PollIntervalSeconds">How often to check it. Optional; defaults to 60.</param>
/// <param name="FailureThreshold">
/// How many consecutive failures declare it offline. Optional; defaults to 3.
/// </param>
/// <param name="SnmpEnabled">
/// Whether the read-only SNMP checks apply. Optional; defaults to false.
/// </param>
/// <param name="SnmpPort">The port those checks use. Optional; defaults to 161.</param>
/// <param name="SnmpCommunity">
/// The read-only community string. Optional, and never returned by any read.
/// </param>
public sealed record RegisterDeviceRequest(
    Guid AssetId,
    string? Hostname,
    string? IpAddress,
    bool? MonitoringEnabled,
    int? PollIntervalSeconds,
    int? FailureThreshold,
    bool? SnmpEnabled,
    int? SnmpPort,
    string? SnmpCommunity);
