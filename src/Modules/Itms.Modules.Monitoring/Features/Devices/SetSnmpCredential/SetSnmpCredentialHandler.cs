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

namespace Itms.Modules.Monitoring.Features.Devices.SetSnmpCredential;

/// <summary>
/// Sets or removes a device's read-only SNMP community string.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything about this handler is arranged so the plaintext has exactly one place to
/// be.</b> It reaches the column and nowhere else: the audit entry records that a
/// credential moved and carries no value on either side of the diff — <c>SetSecret</c>
/// takes no value to pass — and the log line names the device and not the string.
/// The response is 204 with no body, so there is nothing to echo. Reading a device back
/// afterwards answers <c>snmpCredentialSet: true</c> and never the string.
/// </para>
/// <para>
/// <b>Setting one does not compare against the current value.</b> A short-circuit on "it is
/// already that" would let a caller learn the secret by watching which requests move the
/// <c>ETag</c>. Clearing one does compare, because "there was nothing to clear" leaks
/// nothing beyond what the device read already says.
/// </para>
/// </remarks>
/// <param name="database">The monitoring context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
/// <param name="logger">The module's logger.</param>
internal sealed class SetSnmpCredentialHandler(
    MonitoringDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit,
    ILogger<SetSnmpCredentialHandler> logger)
{
    /// <summary>Replaces the device's community string.</summary>
    /// <param name="deviceId">The device to configure.</param>
    /// <param name="request">The credential.</param>
    /// <param name="expectedVersions">
    /// The row versions the caller's <c>If-Match</c> will accept, or <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>Success, or the failure that stopped it.</returns>
    public Task<Result> SetAsync(
        Guid deviceId,
        SetSnmpCredentialRequest request,
        IReadOnlySet<uint>? expectedVersions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return MutateAsync(
            deviceId,
            expectedVersions,
            device =>
            {
                device.SetSnmpCredential(request.Community, clock.UtcNow, currentUser.UserId);
                return true;
            },
            MonitoringAudit.DeviceSnmpCredentialSet,
            MonitoringAudit.Changes().SetSecret("snmpCommunity"),
            MonitoringLog.SnmpCredentialSet,
            cancellationToken);
    }

    /// <summary>Removes the device's community string.</summary>
    /// <param name="deviceId">The device to configure.</param>
    /// <param name="expectedVersions">
    /// The row versions the caller's <c>If-Match</c> will accept, or <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>Success, or the failure that stopped it.</returns>
    public Task<Result> ClearAsync(
        Guid deviceId,
        IReadOnlySet<uint>? expectedVersions,
        CancellationToken cancellationToken) =>
        MutateAsync(
            deviceId,
            expectedVersions,
            device => device.ClearSnmpCredential(clock.UtcNow, currentUser.UserId),
            MonitoringAudit.DeviceSnmpCredentialCleared,
            MonitoringAudit.Changes(),
            MonitoringLog.SnmpCredentialCleared,
            cancellationToken);

    /// <summary>
    /// The load, precondition, save, audit and log every credential write shares.
    /// </summary>
    /// <remarks>
    /// Written once rather than twice, because the two differ only in what they do to the
    /// device and what they call the entry — and the second copy is where a forgotten
    /// precondition check or an audit row carrying the secret would go unnoticed.
    /// </remarks>
    private async Task<Result> MutateAsync(
        Guid deviceId,
        IReadOnlySet<uint>? expectedVersions,
        Func<MonitoredDevice, bool> mutate,
        string action,
        Dictionary<string, AuditFieldChange> changes,
        Action<ILogger, Guid> log,
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

                if (!mutate(device))
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

                await audit.WriteAsync(
                    new AuditEntry(action, MonitoringAudit.DeviceEntityType, device.Id.ToString(), changes),
                    token).ConfigureAwait(false);

                log(logger, device.Id);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? Result.Failure(failure) : Result.Success();
    }
}
