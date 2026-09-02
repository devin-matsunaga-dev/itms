using System.Text.Json.Serialization;

namespace Itms.Modules.Monitoring.Features.Devices.ListDevices;

/// <summary>What the device list is ordered by.</summary>
/// <remarks>
/// A closed set rather than a free-text column name, following <c>AssetSort</c> and
/// <c>TicketSort</c>: a sort that reaches the database as a string is either a table scan
/// on an unindexed column or an injection question nobody wants to have to answer. An
/// unrecognised value is a 400 from model binding, not a silent fallback.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<DeviceSort>))]
public enum DeviceSort
{
    /// <summary>
    /// The tag of the asset the device is. The default, ascending — a monitored estate is
    /// read against the labels on physical equipment, exactly as the asset register is, and
    /// the two lists should run in the same order.
    /// </summary>
    AssetTag,

    /// <summary>
    /// The name the device answers to. Devices with no hostname sort last on the way up,
    /// because "no hostname recorded" is not a name that comes before every other.
    /// </summary>
    Hostname,

    /// <summary>When the device was registered.</summary>
    CreatedAt,

    /// <summary>When its configuration last changed.</summary>
    UpdatedAt,
}
