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
using Microsoft.Extensions.Logging;

namespace Itms.Modules.Monitoring.Features.Devices.SetDeviceMonitoring;

/// <summary>
/// Puts a device under the poller's watch, or takes it off.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own routes rather than a field on the edit form</b>, because it is the one
/// setting somebody will be asked about after an incident: a device nobody was watching is
/// how an outage goes unnoticed. Separating it gives the act its own audit action, so
/// "who turned this off, and when" is a question the trail answers directly rather than
/// one that has to be dug out of a diff among five other fields.
/// </para>
/// <para>
/// <b>Switching to the state it is already in writes nothing and is not an error.</b> The
/// entity answers whether anything moved, and an unmoved device produces no <c>UPDATE</c>,
/// no audit row, and no new <c>ETag</c> — the same call the asset edit makes. It is a
/// 204 either way, because the caller asked for a state and that state is what holds.
/// </para>
/// </remarks>
/// <param name="database">The monitoring context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
/// <param name="logger">The module's logger.</param>
internal sealed class SetDeviceMonitoringHandler(
    MonitoringDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit,
    ILogger<SetDeviceMonitoringHandler> logger)
{
    /// <summary>Sets whether the poller picks the device up.</summary>
    /// <param name="deviceId">The device to switch.</param>
    /// <param name="enabled">True to have it watched.</param>
    /// <param name="expectedVersions">
    /// The row versions the caller's <c>If-Match</c> will accept, or <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>Success, or the failure that stopped it.</returns>
    public async Task<Result> HandleAsync(
        Guid deviceId,
        bool enabled,
        IReadOnlySet<uint>? expectedVersions,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                var device = await database.Devices
                    .FirstOrDefaultAsync(candidate => candidate.Id == deviceId, token)
                    .ConfigureAwait(false);

                if (device is null)
                {
                    failure = MonitoringErrors.DeviceNotFound();
                    return;
                }

                if (expectedVersions is not null
                    && !expectedVersions.Contains(
                        database.Entry(device)
                            .Property<uint>(MonitoredDeviceConfiguration.VersionProperty).CurrentValue))
                {
                    failure = MonitoringErrors.DevicePreconditionFailed();
                    return;
                }

                if (!device.SetMonitoringEnabled(enabled, clock.UtcNow, currentUser.UserId))
                {
                    return;
                }

                try
                {
                    await database.SaveChangesAsync(token).ConfigureAwait(false);
                }
                catch (DbUpdateConcurrencyException)
                {
                    failure = MonitoringErrors.DeviceConflict();
                    return;
                }

                // No diff: the action name carries the whole fact, and a before/after of
                // false→true would say the same thing twice.
                await audit.WriteAsync(
                    new AuditEntry(
                        enabled
                            ? MonitoringAudit.DeviceMonitoringEnabled
                            : MonitoringAudit.DeviceMonitoringDisabled,
                        MonitoringAudit.DeviceEntityType,
                        device.Id.ToString(),
                        MonitoringAudit.Changes()),
                    token).ConfigureAwait(false);

                MonitoringLog.DeviceMonitoringChanged(logger, device.Id, enabled);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? Result.Failure(failure) : Result.Success();
    }
}
