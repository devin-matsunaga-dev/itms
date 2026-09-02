namespace Itms.Modules.Monitoring.Features.Devices.UpdateDevice;

/// <summary>
/// The fields a monitored device's configuration is corrected from.
/// </summary>
/// <remarks>
/// <para>
/// <b>A full replacement: a field omitted from the body is cleared, not left alone.</b>
/// That is the honest reading of <c>PUT</c> and the call WP-2.6b made for an asset — the
/// edit form holds every field and posts all of them, so "the operator emptied the hostname
/// box" and "the client forgot hostname" are the same request and must mean the same thing.
/// </para>
/// <para>
/// <b>Three things are deliberately absent rather than validated away.</b> There is no
/// <c>assetId</c>: a device is a projection over one asset (invariant 6) and re-pointing it
/// would make every result already filed against it describe a different machine. There is
/// no <c>monitoringEnabled</c>: switching monitoring off is an operational act with its own
/// audit line, not a correction, and it has its own two routes. And there is no
/// <c>snmpCommunity</c>: applied to a write-only secret, the full-replacement rule above
/// would silently wipe a credential the form was never given, so the credential has its own
/// two routes as well.
/// </para>
/// </remarks>
/// <param name="Hostname">
/// The name the device answers to. Optional, but a device needs this or an address.
/// </param>
/// <param name="IpAddress">
/// The address it is polled at, as text. Optional, but a device needs this or a hostname.
/// </param>
/// <param name="PollIntervalSeconds">How often to check it. Optional; defaults to 60.</param>
/// <param name="FailureThreshold">
/// How many consecutive failures declare it offline. Optional; defaults to 3.
/// </param>
/// <param name="SnmpEnabled">Whether the read-only SNMP checks apply. Optional; defaults to false.</param>
/// <param name="SnmpPort">The port those checks use. Optional; defaults to 161.</param>
public sealed record UpdateDeviceRequest(
    string? Hostname,
    string? IpAddress,
    int? PollIntervalSeconds,
    int? FailureThreshold,
    bool? SnmpEnabled,
    int? SnmpPort);
