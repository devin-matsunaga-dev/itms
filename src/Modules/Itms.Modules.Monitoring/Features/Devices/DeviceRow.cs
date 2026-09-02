using System.Linq.Expressions;
using System.Net;
using Itms.Modules.Monitoring.Domain;
using Itms.Modules.Monitoring.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Monitoring.Features.Devices;

/// <summary>
/// The columns a device read actually selects, before they are shaped into a response.
/// </summary>
/// <remarks>
/// <para>
/// <b>It exists because two of the response's fields are not columns.</b> The address is
/// <c>inet</c> in the database and a string on the wire, and rendering it means calling
/// <see cref="IPAddress.ToString"/>, which is C# the database cannot run. And
/// <c>snmpCredentialSet</c> is a question about the secret rather than the secret — the
/// <c>!= null</c> is evaluated in SQL, so the community string is never among the values
/// the query returns and the plaintext does not cross the connection on any read at all.
/// That is a stronger guarantee than a response type that merely declines to carry it.
/// </para>
/// <para>
/// One projection shared by the detail read and the list, unlike the asset reads which
/// need two: an asset's list row is deliberately narrower than its detail because cost and
/// notes belong on one screen and not the other. A device has no such field — everything
/// about it is configuration somebody scanning the register wants to see — so a second,
/// narrower shape would be two things to keep agreeing for no benefit.
/// </para>
/// </remarks>
/// <param name="Id">The device's id.</param>
/// <param name="AssetId">The asset it is.</param>
/// <param name="AssetTag">That asset's tag.</param>
/// <param name="Hostname">The name it answers to.</param>
/// <param name="IpAddress">The address it is polled at.</param>
/// <param name="MonitoringEnabled">Whether the poller picks it up.</param>
/// <param name="PollIntervalSeconds">How often it is checked.</param>
/// <param name="FailureThreshold">How many consecutive failures declare it offline.</param>
/// <param name="SnmpEnabled">Whether the read-only SNMP checks apply.</param>
/// <param name="SnmpPort">The port those checks use.</param>
/// <param name="SnmpCredentialSet">Whether a community string is configured.</param>
/// <param name="CreatedAt">When the device was registered (UTC).</param>
/// <param name="UpdatedAt">When it last changed (UTC).</param>
/// <param name="Version">The <c>xmin</c> row version.</param>
internal sealed record DeviceRow(
    Guid Id,
    Guid AssetId,
    string AssetTag,
    string? Hostname,
    IPAddress? IpAddress,
    bool MonitoringEnabled,
    int PollIntervalSeconds,
    int FailureThreshold,
    bool SnmpEnabled,
    int SnmpPort,
    bool SnmpCredentialSet,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version)
{
    /// <summary>The projection every device read uses.</summary>
    /// <returns>The expression EF translates into the select list.</returns>
    public static Expression<Func<MonitoredDevice, DeviceRow>> Projection() =>
        device => new DeviceRow(
            device.Id,
            device.AssetId,
            device.AssetTag,
            device.Hostname,
            device.IpAddress,
            device.MonitoringEnabled,
            device.PollIntervalSeconds,
            device.FailureThreshold,
            device.SnmpEnabled,
            device.SnmpPort,
            device.SnmpCommunity != null,
            device.CreatedAt,
            device.UpdatedAt,
            EF.Property<uint>(device, MonitoredDeviceConfiguration.VersionProperty));

    /// <summary>Shapes the row into the response the API answers with.</summary>
    /// <returns>The response shape.</returns>
    public DeviceResponse ToResponse() =>
        new(
            Id,
            AssetId,
            AssetTag,
            Hostname,
            IpAddress?.ToString(),
            MonitoringEnabled,
            PollIntervalSeconds,
            FailureThreshold,
            SnmpEnabled,
            SnmpPort,
            SnmpCredentialSet,
            CreatedAt,
            UpdatedAt);

    /// <summary>Shapes the row into the response and the version that came back with it.</summary>
    /// <returns>The response and its <c>ETag</c> version.</returns>
    public DeviceDetail ToDetail() => new(ToResponse(), Version);
}
