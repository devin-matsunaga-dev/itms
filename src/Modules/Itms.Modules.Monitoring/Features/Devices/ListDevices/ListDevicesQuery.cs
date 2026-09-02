using Itms.Platform.Paging;
using Microsoft.AspNetCore.Mvc;

namespace Itms.Modules.Monitoring.Features.Devices.ListDevices;

/// <summary>The query string the device list accepts.</summary>
/// <remarks>
/// Deliberately much narrower than the asset register's. A device carries no department, no
/// location and no holder of its own — those are facts about the asset it is, and asking
/// for them here would mean either a second cached copy of each or a join across a module
/// boundary, both of which §3 forbids. Somebody filtering monitored equipment by room does
/// it on the asset register; this list answers the two questions that are the device's own:
/// which of them are watched, and where is the one I am looking for.
/// </remarks>
public sealed class ListDevicesQuery
{
    /// <summary>Only devices the poller does, or does not, pick up. Omitted, both.</summary>
    [FromQuery(Name = "monitoringEnabled")]
    public bool? MonitoringEnabled { get; init; }

    /// <summary>Only devices with the read-only SNMP checks on, or off. Omitted, both.</summary>
    [FromQuery(Name = "snmpEnabled")]
    public bool? SnmpEnabled { get; init; }

    /// <summary>
    /// Free text matched against the hostname and the asset tag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A case-insensitive "contains" over two columns of one table, exactly as the asset
    /// register's and the ticket queue's are — not the cross-entity search <c>WP-4.2</c>
    /// builds.
    /// </para>
    /// <para>
    /// <b>This is where a hostname first becomes searchable, and it is not where WP-2.3
    /// expected it.</b> WP-2.3 left a note saying the package that gives an asset a
    /// hostname adds it to the <em>asset</em> register's search. That cannot happen: the
    /// hostname is a device's field in the monitoring schema, and <c>ListAssetsHandler</c>
    /// lives in Assets, which may not query it (§3 rule 1). Searching assets by hostname is
    /// therefore a cross-entity question, which is <c>WP-4.2</c>'s by definition — the note
    /// in <c>ListAssetsQuery.Search</c> has been corrected to say so.
    /// </para>
    /// <para>
    /// The address is not searched. <c>inet</c> is not text, a <c>LIKE</c> over it would
    /// mean a cast that no index can serve, and "starts with 10.1." is a subnet question
    /// that <c>inet</c>'s own containment operators answer properly. Nothing has asked for
    /// one yet.
    /// </para>
    /// </remarks>
    [FromQuery(Name = "search")]
    public string? Search { get; init; }

    /// <summary>What to order by. Defaults to <see cref="DeviceSort.AssetTag"/>.</summary>
    [FromQuery(Name = "sort")]
    public DeviceSort? Sort { get; init; }

    /// <summary>Which way to order. Defaults to ascending for the two name orderings and descending otherwise.</summary>
    [FromQuery(Name = "direction")]
    public SortDirection? Direction { get; init; }

    /// <summary>The 1-based page number. Out-of-range values are clamped, not rejected.</summary>
    [FromQuery(Name = "page")]
    public int? Page { get; init; }

    /// <summary>How many devices to a page, up to the API-wide maximum of 200.</summary>
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; }
}
