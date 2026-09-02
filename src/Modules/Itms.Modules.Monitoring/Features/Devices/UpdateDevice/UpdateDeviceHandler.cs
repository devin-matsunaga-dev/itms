using System.Globalization;
using Itms.Contracts.Auditing;
using Itms.Modules.Monitoring.Auditing;
using Itms.Modules.Monitoring.Domain;
using Itms.Modules.Monitoring.Persistence;
using Itms.Modules.Monitoring.Persistence.Configurations;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Monitoring.Features.Devices.UpdateDevice;

/// <summary>
/// Corrects where a device is reached and how it is polled.
/// </summary>
/// <remarks>
/// <para>
/// <b>An edit that moves nothing writes nothing — no <c>UPDATE</c>, no audit row, no new
/// <c>ETag</c>.</b> <c>MonitoredDevice.Update</c> compares the normalised settings against
/// what the device already carries by record value and returns early, so a form
/// re-submitted unchanged leaves <c>xmin</c> alone rather than refusing every other
/// reader's precondition with a 412 for a change that never happened. The audit row follows
/// the database: if the row did not move, nothing is claimed to have.
/// </para>
/// <para>
/// <b>It cannot reach the SNMP credential or the monitoring switch</b> — see
/// <see cref="UpdateDeviceRequest"/>. The enforcement is structural rather than remembered:
/// <c>DeviceSettings</c> has no field for either, so there is nothing here to validate away.
/// </para>
/// </remarks>
/// <param name="database">The monitoring context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
internal sealed class UpdateDeviceHandler(
    MonitoringDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit)
{
    /// <summary>Applies <paramref name="request"/> to the device with <paramref name="deviceId"/>.</summary>
    /// <param name="deviceId">The device being corrected.</param>
    /// <param name="request">The settings as they should now read.</param>
    /// <param name="expectedVersions">
    /// The row versions the caller's <c>If-Match</c> will accept, or <see langword="null"/>
    /// when it stated no precondition.
    /// </param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>The device as it now stands, or the failure that stopped the edit.</returns>
    public async Task<Result<DeviceDetail>> HandleAsync(
        Guid deviceId,
        UpdateDeviceRequest request,
        IReadOnlySet<uint>? expectedVersions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!DeviceAddress.TryParse(request.IpAddress, out var address))
        {
            return MonitoringErrors.MalformedIpAddress();
        }

        if (string.IsNullOrWhiteSpace(request.Hostname) && address is null)
        {
            return MonitoringErrors.DeviceUnreachable();
        }

        Error? failure = null;
        DeviceDetail? updated = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                // Tracked, not AsNoTracking: this is a write, and the xmin token only does
                // its job on a tracked entity.
                var device = await database.Devices
                    .FirstOrDefaultAsync(candidate => candidate.Id == deviceId, token)
                    .ConfigureAwait(false);

                if (device is null)
                {
                    failure = MonitoringErrors.DeviceNotFound();
                    return;
                }

                var entry = database.Entry(device);

                // The caller's precondition, checked before anything is attempted — the
                // whole point of the 412. The row is already loaded by the read, so this
                // cannot itself race.
                if (expectedVersions is not null
                    && !expectedVersions.Contains(
                        entry.Property<uint>(MonitoredDeviceConfiguration.VersionProperty).CurrentValue))
                {
                    failure = MonitoringErrors.DevicePreconditionFailed();
                    return;
                }

                var before = DeviceSettings.Of(device);

                var after = device.Update(
                    new DeviceSettings(
                        request.Hostname,
                        address,
                        request.PollIntervalSeconds ?? MonitoredDevice.DefaultPollIntervalSeconds,
                        request.FailureThreshold ?? MonitoredDevice.DefaultFailureThreshold,
                        request.SnmpEnabled ?? false,
                        request.SnmpPort ?? SnmpSettings.DefaultPort),
                    clock.UtcNow,
                    currentUser.UserId);

                if (before != after)
                {
                    try
                    {
                        await database.SaveChangesAsync(token).ConfigureAwait(false);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        // Somebody moved the device between the read and the write. A 409
                        // the client can retry, not the 500 an unhandled one would be.
                        failure = MonitoringErrors.DeviceConflict();
                        return;
                    }

                    await audit.WriteAsync(
                        new AuditEntry(
                            MonitoringAudit.DeviceUpdated,
                            MonitoringAudit.DeviceEntityType,
                            device.Id.ToString(),
                            MonitoringAudit.Changes()
                                .Moved("hostname", before.Hostname, after.Hostname)
                                .Moved("ipAddress", before.IpAddress?.ToString(), after.IpAddress?.ToString())
                                .Moved(
                                    "pollIntervalSeconds",
                                    before.PollIntervalSeconds.ToString(CultureInfo.InvariantCulture),
                                    after.PollIntervalSeconds.ToString(CultureInfo.InvariantCulture))
                                .Moved(
                                    "failureThreshold",
                                    before.FailureThreshold.ToString(CultureInfo.InvariantCulture),
                                    after.FailureThreshold.ToString(CultureInfo.InvariantCulture))
                                .Moved(
                                    "snmpEnabled",
                                    before.SnmpEnabled.ToString(CultureInfo.InvariantCulture),
                                    after.SnmpEnabled.ToString(CultureInfo.InvariantCulture))
                                .Moved(
                                    "snmpPort",
                                    before.SnmpPort.ToString(CultureInfo.InvariantCulture),
                                    after.SnmpPort.ToString(CultureInfo.InvariantCulture))),
                        token).ConfigureAwait(false);
                }

                updated = new DeviceDetail(
                    DeviceResponse.From(device),
                    // Read back off the tracked entry after the write: xmin is
                    // ValueGeneratedOnAddOrUpdate, so EF refreshes it with the UPDATE. An
                    // edit that changed nothing issues no UPDATE and the tag is unchanged,
                    // which is the honest answer — nobody else's precondition was broken.
                    entry.Property<uint>(MonitoredDeviceConfiguration.VersionProperty).CurrentValue);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? failure : updated!;
    }
}
