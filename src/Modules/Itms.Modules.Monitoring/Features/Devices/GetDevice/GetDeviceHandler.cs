using Itms.Modules.Monitoring.Domain;
using Itms.Modules.Monitoring.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Monitoring.Features.Devices.GetDevice;

/// <summary>
/// Reads one monitored device.
/// </summary>
/// <remarks>
/// Projected in the query rather than by loading the entity and mapping it
/// (CONVENTIONS.md). See <c>DeviceRow</c> for why that projection is also what keeps the
/// SNMP community string off the connection entirely on a read.
/// </remarks>
/// <param name="database">The monitoring context.</param>
internal sealed class GetDeviceHandler(MonitoringDbContext database)
{
    /// <summary>Reads the device with <paramref name="deviceId"/>.</summary>
    /// <param name="deviceId">The device to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The device and its version, or a 404.</returns>
    public async Task<Result<DeviceDetail>> HandleAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var row = await database.Devices
            .AsNoTracking()
            .Where(candidate => candidate.Id == deviceId)
            .Select(DeviceRow.Projection())
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null ? MonitoringErrors.DeviceNotFound() : row.ToDetail();
    }
}
