using Itms.Modules.Monitoring.Domain;

namespace Itms.Modules.Monitoring.Features.Devices;

/// <summary>
/// A monitored device as the API renders it.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no community string on this shape and there must never be one.</b> The
/// credential is write-only: it goes in through
/// <c>PUT /api/v1/devices/{id}/snmp-credential</c> and leaves the database only through
/// <c>WP-3.2</c>'s authenticated configuration pull, which is a different endpoint with a
/// different caller and a different credential. What a screen needs is whether one is
/// configured, and <see cref="SnmpCredentialSet"/> is that — a boolean carries the whole of
/// the useful information and none of the secret.
/// </para>
/// <para>
/// <b><see cref="IpAddress"/> is a string here and an <c>IPAddress</c> in the entity.</b>
/// The column is PostgreSQL's <c>inet</c>, which is what refuses a value that is not an
/// address; the conversion happens at this edge so the contract describes a plain string
/// and the generated client has nothing unusual to model.
/// </para>
/// <para>
/// <b>The asset tag is cached on the device row rather than looked up per read.</b> Unlike
/// every other cached display string in this system it cannot go stale — invariant 4 makes
/// an asset tag immutable — so there is no refresh consumer owed here. Following the tag to
/// the asset itself is <c>GET /api/v1/assets/{id}</c> with <see cref="AssetId"/>.
/// </para>
/// </remarks>
/// <param name="Id">The device's id.</param>
/// <param name="AssetId">The asset this device is (invariant 6).</param>
/// <param name="AssetTag">That asset's tag.</param>
/// <param name="Hostname">The name the device answers to.</param>
/// <param name="IpAddress">The address it is polled at.</param>
/// <param name="MonitoringEnabled">Whether the poller picks it up.</param>
/// <param name="PollIntervalSeconds">How often it is checked.</param>
/// <param name="FailureThreshold">How many consecutive failures declare it offline.</param>
/// <param name="SnmpEnabled">Whether the read-only SNMP checks apply.</param>
/// <param name="SnmpPort">The port those checks use.</param>
/// <param name="SnmpCredentialSet">
/// Whether a community string is configured. The string itself is never returned.
/// </param>
/// <param name="CreatedAt">When the device was registered (UTC).</param>
/// <param name="UpdatedAt">When it last changed (UTC).</param>
public sealed record DeviceResponse(
    Guid Id,
    Guid AssetId,
    string AssetTag,
    string? Hostname,
    string? IpAddress,
    bool MonitoringEnabled,
    int PollIntervalSeconds,
    int FailureThreshold,
    bool SnmpEnabled,
    int SnmpPort,
    bool SnmpCredentialSet,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Renders <paramref name="device"/>.</summary>
    /// <param name="device">The device to render.</param>
    /// <returns>The response shape.</returns>
    public static DeviceResponse From(MonitoredDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return new DeviceResponse(
            device.Id,
            device.AssetId,
            device.AssetTag,
            device.Hostname,
            device.IpAddress?.ToString(),
            device.MonitoringEnabled,
            device.PollIntervalSeconds,
            device.FailureThreshold,
            device.SnmpEnabled,
            device.SnmpPort,
            device.HasSnmpCredential,
            device.CreatedAt,
            device.UpdatedAt);
    }
}
