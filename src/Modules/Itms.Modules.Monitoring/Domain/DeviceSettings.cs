using System.Net;

namespace Itms.Modules.Monitoring.Domain;

/// <summary>
/// The half of a monitored device an administrator may correct in one go: where it is
/// reached, how often, and how patiently.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no field for the asset, and that is the enforcement of invariant 6.</b> A
/// device is a projection over one asset; re-pointing it at a different asset would make
/// every check result, outage and alert already filed against it describe a different
/// machine. There is nothing here to ignore and no way for a caller to be surprised — the
/// asset a device is cannot be changed because no code path exists that would change it.
/// </para>
/// <para>
/// <b>There is no field for the community string either, and that is deliberate for a
/// different reason.</b> <c>PUT</c> is a full replacement — a field omitted from the body
/// is cleared, which is the honest reading of the verb and the call WP-2.6b made for an
/// asset. Applied to a secret the API never gives back, that rule would be a trap: an
/// administrator correcting a hostname on a form that (correctly) never received the
/// community string would silently wipe the device's SNMP credential. So the credential
/// moves through its own two routes, and this shape cannot reach it.
/// </para>
/// <para>
/// <b>And there is no field for <c>MonitoringEnabled</c>.</b> Turning monitoring off is an
/// operational act with its own audit line, not a correction — the same reason an asset's
/// status moves through the lifecycle routes rather than through its edit form.
/// </para>
/// <para>
/// Compared by record value, so a field added here joins
/// <see cref="MonitoredDevice.Update"/>'s "did anything actually move" check without being
/// named there. <see cref="IPAddress"/> compares by value, so the comparison is sound.
/// </para>
/// </remarks>
/// <param name="Hostname">The name the device answers to, or <see langword="null"/>.</param>
/// <param name="IpAddress">The address it is polled at, or <see langword="null"/>.</param>
/// <param name="PollIntervalSeconds">How often to check it.</param>
/// <param name="FailureThreshold">How many consecutive failures declare it offline.</param>
/// <param name="SnmpEnabled">Whether the read-only SNMP checks apply.</param>
/// <param name="SnmpPort">The port those checks use.</param>
public sealed record DeviceSettings(
    string? Hostname,
    IPAddress? IpAddress,
    int PollIntervalSeconds,
    int FailureThreshold,
    bool SnmpEnabled,
    int SnmpPort)
{
    /// <summary>The settings <paramref name="device"/> currently carries — the "before" half of a diff.</summary>
    /// <param name="device">The device to read.</param>
    /// <returns>Its current settings.</returns>
    public static DeviceSettings Of(MonitoredDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return new DeviceSettings(
            device.Hostname,
            device.IpAddress,
            device.PollIntervalSeconds,
            device.FailureThreshold,
            device.SnmpEnabled,
            device.SnmpPort);
    }
}
