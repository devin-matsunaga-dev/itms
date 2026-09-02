using Microsoft.Extensions.Logging;

namespace Itms.Modules.Monitoring;

/// <summary>
/// The module's log messages, source-generated. CONVENTIONS.md requires structured
/// properties, and the repo builds warnings-as-errors with CA1848 on, so every message is
/// declared here rather than formatted at the call site.
/// </summary>
/// <remarks>
/// <b>No message here takes a community string, and none ever may.</b> CONVENTIONS.md names
/// SNMP community strings alongside passwords and session cookies in the list of things
/// never to log, and the credential routes are exactly where a well-meant "what did we just
/// write" line would end up. A device is identified by its id and its asset tag; that is
/// enough to trace any of these operations.
/// </remarks>
internal static partial class MonitoringLog
{
    [LoggerMessage(
        EventId = 3100,
        Level = LogLevel.Information,
        Message = "Registered monitored device {DeviceId} over asset {AssetTag}.")]
    public static partial void DeviceRegistered(ILogger logger, Guid deviceId, string assetTag);

    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Information,
        Message = "Monitoring for device {DeviceId} is now {MonitoringEnabled}.")]
    public static partial void DeviceMonitoringChanged(ILogger logger, Guid deviceId, bool monitoringEnabled);

    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Information,
        Message = "The SNMP credential for device {DeviceId} was replaced.")]
    public static partial void SnmpCredentialSet(ILogger logger, Guid deviceId);

    [LoggerMessage(
        EventId = 3103,
        Level = LogLevel.Information,
        Message = "The SNMP credential for device {DeviceId} was removed.")]
    public static partial void SnmpCredentialCleared(ILogger logger, Guid deviceId);
}
