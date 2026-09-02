using System.Globalization;
using Itms.Contracts.Auditing;
using Itms.Contracts.Lookups;
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

namespace Itms.Modules.Monitoring.Features.Devices.RegisterDevice;

/// <summary>
/// Registers a monitored device over an asset.
/// </summary>
/// <remarks>
/// <para>
/// <b>This handler is where invariant 6 is enforced, and it is the whole of WP-3.1's
/// done-criterion.</b> The asset is resolved through <c>IAssetLookup</c> — the public
/// contract ARCHITECTURE.md §3 rule 2 requires, because Monitoring may not reference
/// <c>Modules.Assets</c> or query the assets schema — and a device is built only from what
/// that lookup answered. There is no branch in which an unresolved asset produces a device:
/// monitoring cannot create device records of its own.
/// </para>
/// <para>
/// A soft-deleted asset resolves to <see langword="null"/> through the lookup, so writing
/// off equipment also stops it being newly monitored. It does not stop an existing device
/// from being monitored, which is a question <c>WP-3.3</c> or an asset delete path will have
/// to answer; no work package owns it yet.
/// </para>
/// <para>
/// The uniqueness check is advisory — a concurrent insert can still beat it to
/// <c>ux_devices_asset_id</c> — and exists so the common case comes back as a 409 naming
/// the asset rather than as a database exception. The index behind it is what makes the
/// rare case safe.
/// </para>
/// </remarks>
/// <param name="database">The monitoring context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
/// <param name="assets">How this module reads the asset a device is.</param>
/// <param name="logger">The module's logger.</param>
internal sealed class RegisterDeviceHandler(
    MonitoringDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IAssetLookup assets,
    ILogger<RegisterDeviceHandler> logger)
{
    /// <summary>Registers the device described by <paramref name="request"/>.</summary>
    /// <param name="request">The new device's fields.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The device and its version, or the failure that refused it.</returns>
    public async Task<Result<DeviceDetail>> HandleAsync(
        RegisterDeviceRequest request,
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

        // Resolved before the transaction opens: this is a read against another module, and
        // holding a write transaction across it would widen it for no benefit. It is also
        // the invariant — no asset, no device.
        var asset = await assets.GetAsync(request.AssetId, cancellationToken).ConfigureAwait(false);

        if (asset is null)
        {
            return MonitoringErrors.AssetNotFound();
        }

        Error? failure = null;
        DeviceDetail? registered = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                var alreadyMonitored = await database.Devices
                    .AsNoTracking()
                    .AnyAsync(candidate => candidate.AssetId == asset.Id, token)
                    .ConfigureAwait(false);

                if (alreadyMonitored)
                {
                    failure = MonitoringErrors.DeviceAlreadyRegistered(asset.AssetTag);
                    return;
                }

                var device = MonitoredDevice.Register(
                    new NewDevice(
                        asset.Id,
                        asset.AssetTag,
                        request.Hostname,
                        address,
                        request.PollIntervalSeconds ?? MonitoredDevice.DefaultPollIntervalSeconds,
                        request.FailureThreshold ?? MonitoredDevice.DefaultFailureThreshold,
                        // Registering a device is asking for it to be watched, so the
                        // default is on. Somebody preparing a device ahead of a cutover can
                        // say otherwise.
                        request.MonitoringEnabled ?? true,
                        request.SnmpEnabled ?? false,
                        request.SnmpPort ?? SnmpSettings.DefaultPort,
                        request.SnmpCommunity),
                    clock.UtcNow,
                    currentUser.UserId);

                var entry = database.Devices.Add(device);
                await database.SaveChangesAsync(token).ConfigureAwait(false);

                // Inside the transaction, so a registration that rolls back leaves no entry
                // claiming it happened. SPEC.md §15 makes administrative configuration
                // changes mandatory audit coverage; ARCHITECTURE.md §5 names no
                // DeviceRegistered event, so this goes through IAuditWriter.
                //
                // The community string is recorded as having been set and never as its
                // value — see MonitoringAudit, and SetSecret, which takes no value to pass.
                var changes = MonitoringAudit.Changes()
                    .Set("assetId", device.AssetId.ToString())
                    .Set("assetTag", device.AssetTag)
                    .Set("hostname", device.Hostname)
                    .Set("ipAddress", device.IpAddress?.ToString())
                    .Set("monitoringEnabled", device.MonitoringEnabled.ToString(CultureInfo.InvariantCulture))
                    .Set("pollIntervalSeconds", device.PollIntervalSeconds.ToString(CultureInfo.InvariantCulture))
                    .Set("failureThreshold", device.FailureThreshold.ToString(CultureInfo.InvariantCulture))
                    .Set("snmpEnabled", device.SnmpEnabled.ToString(CultureInfo.InvariantCulture))
                    .Set("snmpPort", device.SnmpPort.ToString(CultureInfo.InvariantCulture));

                if (device.HasSnmpCredential)
                {
                    changes.SetSecret("snmpCommunity");
                }

                await audit.WriteAsync(
                    new AuditEntry(
                        MonitoringAudit.DeviceRegistered,
                        MonitoringAudit.DeviceEntityType,
                        device.Id.ToString(),
                        changes),
                    token).ConfigureAwait(false);

                MonitoringLog.DeviceRegistered(logger, device.Id, device.AssetTag);

                registered = new DeviceDetail(
                    DeviceResponse.From(device),
                    // Read back off the tracked entry after the INSERT: xmin is
                    // ValueGeneratedOnAddOrUpdate, so this is the version the ETag on the
                    // 201 names, and it is what a client puts in the If-Match of the first
                    // edit it makes against the device it just registered.
                    entry.Property<uint>(MonitoredDeviceConfiguration.VersionProperty).CurrentValue);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? failure : registered!;
    }
}
