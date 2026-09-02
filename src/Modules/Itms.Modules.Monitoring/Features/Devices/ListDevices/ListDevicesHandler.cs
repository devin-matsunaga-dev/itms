using Itms.Modules.Monitoring.Domain;
using Itms.Modules.Monitoring.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Monitoring.Features.Devices.ListDevices;

/// <summary>Reads the monitored-device register, filtered, sorted, and paged.</summary>
/// <param name="database">The monitoring context.</param>
internal sealed class ListDevicesHandler(MonitoringDbContext database)
{
    /// <summary>Reads a page of devices.</summary>
    /// <param name="query">The filters, ordering, and page.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The page envelope.</returns>
    public async Task<Result<PagedResult<DeviceResponse>>> HandleAsync(
        ListDevicesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = PageRequest.Of(query.Page, query.PageSize);
        var devices = Filter(database.Devices.AsNoTracking(), query);

        var total = await devices.CountAsync(cancellationToken).ConfigureAwait(false);

        if (total == 0)
        {
            return PagedResult.Empty<DeviceResponse>(page);
        }

        // Ordered before it is projected: EF cannot translate an OrderBy over a
        // constructed record, and the tie-break has to reach real columns.
        var rows = await Order(devices, query)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(DeviceRow.Projection())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.From<DeviceResponse>(
            [.. rows.Select(row => row.ToResponse())],
            total,
            page);
    }

    private static IQueryable<MonitoredDevice> Filter(
        IQueryable<MonitoredDevice> devices,
        ListDevicesQuery query)
    {
        if (query.MonitoringEnabled is { } monitoringEnabled)
        {
            devices = devices.Where(device => device.MonitoringEnabled == monitoringEnabled);
        }

        if (query.SnmpEnabled is { } snmpEnabled)
        {
            devices = devices.Where(device => device.SnmpEnabled == snmpEnabled);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // The escaping is the shared kernel's (WP-1.12): an unescaped % or _ typed into
            // the box would otherwise become a wildcard over the whole table.
            var pattern = SearchPattern.Containing(query.Search);

            devices = devices.Where(device =>
                EF.Functions.ILike(device.AssetTag, pattern, SearchPattern.Escape)
                || (device.Hostname != null
                    && EF.Functions.ILike(device.Hostname, pattern, SearchPattern.Escape)));
        }

        return devices;
    }

    /// <summary>
    /// Applies the ordering, always ending on the id.
    /// </summary>
    /// <remarks>
    /// The final <c>ThenBy</c> on the id is what makes paging stable: none of the four sort
    /// columns is unique, and two devices sharing a value would otherwise be free to swap
    /// places between page one and page two and be shown twice or not at all.
    /// </remarks>
    private static IQueryable<MonitoredDevice> Order(
        IQueryable<MonitoredDevice> devices,
        ListDevicesQuery query)
    {
        var sort = query.Sort ?? DeviceSort.AssetTag;

        // The two name orderings read forwards; the two timestamps read newest-first,
        // because "what changed lately" is the question a date column is asked.
        var ascending = (query.Direction ?? DefaultDirectionFor(sort)) == SortDirection.Ascending;

        return sort switch
        {
            DeviceSort.Hostname => ascending
                // Devices with no hostname sort last on the way up: "no hostname recorded"
                // is not a name that comes before every other. PostgreSQL puts NULLs last
                // on ASC by default, which is already what is wanted, and first on DESC,
                // which is also right — the ordering is simply reversed.
                ? devices.OrderBy(device => device.NormalizedHostname).ThenBy(device => device.Id)
                : devices.OrderByDescending(device => device.NormalizedHostname).ThenBy(device => device.Id),
            DeviceSort.CreatedAt => ascending
                ? devices.OrderBy(device => device.CreatedAt).ThenBy(device => device.Id)
                : devices.OrderByDescending(device => device.CreatedAt).ThenBy(device => device.Id),
            DeviceSort.UpdatedAt => ascending
                ? devices.OrderBy(device => device.UpdatedAt).ThenBy(device => device.Id)
                : devices.OrderByDescending(device => device.UpdatedAt).ThenBy(device => device.Id),
            _ => ascending
                ? devices.OrderBy(device => device.AssetTag).ThenBy(device => device.Id)
                : devices.OrderByDescending(device => device.AssetTag).ThenBy(device => device.Id),
        };
    }

    private static SortDirection DefaultDirectionFor(DeviceSort sort) => sort switch
    {
        DeviceSort.AssetTag or DeviceSort.Hostname => SortDirection.Ascending,
        _ => SortDirection.Descending,
    };
}
