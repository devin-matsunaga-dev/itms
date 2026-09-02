using System.Net;

namespace Itms.Modules.Monitoring.Domain;

/// <summary>
/// The facts a monitored device is registered from.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="AssetId"/> and <see cref="AssetTag"/> arrive together, and neither is the
/// caller's to invent.</b> Invariant 6 says a monitored device is always an asset and
/// monitoring cannot create device records of its own, so both fields are filled from what
/// <c>IAssetLookup</c> answered — the handler resolves the asset first and this record
/// cannot be built without one. That is the structural half of the invariant; the unique
/// index on <c>asset_id</c> is the other half.
/// </para>
/// <para>
/// <b>The community string is here and is the only secret in the shape.</b> It is optional
/// at registration, and once written it never comes back out through a read — see
/// <see cref="MonitoredDevice.SnmpCommunity"/>.
/// </para>
/// </remarks>
/// <param name="AssetId">The asset this device is, as resolved through <c>IAssetLookup</c>.</param>
/// <param name="AssetTag">That asset's tag, cached per §3 rule 6.</param>
/// <param name="Hostname">The name the device answers to, or <see langword="null"/>.</param>
/// <param name="IpAddress">The address it is polled at, or <see langword="null"/>.</param>
/// <param name="PollIntervalSeconds">How often to check it.</param>
/// <param name="FailureThreshold">How many consecutive failures declare it offline.</param>
/// <param name="MonitoringEnabled">Whether the poller should pick it up at all.</param>
/// <param name="SnmpEnabled">Whether the read-only SNMP checks apply.</param>
/// <param name="SnmpPort">The port those checks use.</param>
/// <param name="SnmpCommunity">The read-only community string, or <see langword="null"/>.</param>
public sealed record NewDevice(
    Guid AssetId,
    string AssetTag,
    string? Hostname,
    IPAddress? IpAddress,
    int PollIntervalSeconds,
    int FailureThreshold,
    bool MonitoringEnabled,
    bool SnmpEnabled,
    int SnmpPort,
    string? SnmpCommunity);
