using Itms.Modules.Monitoring.Features.Devices.GetDevice;
using Itms.Modules.Monitoring.Features.Devices.ListDevices;
using Itms.Modules.Monitoring.Features.Devices.RegisterDevice;
using Itms.Modules.Monitoring.Features.Devices.SetDeviceMonitoring;
using Itms.Modules.Monitoring.Features.Devices.SetSnmpCredential;
using Itms.Modules.Monitoring.Features.Devices.UpdateDevice;
using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Itms.Platform.Security;
using Itms.Platform.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MinimalApi = Microsoft.AspNetCore.Http.Results;

namespace Itms.Modules.Monitoring.Features.Devices;

/// <summary>The monitored-device endpoints, under <c>/api/v1/devices</c>.</summary>
/// <remarks>
/// <para>
/// <b>Reads are Technician-or-Admin; every write is Admin.</b> SPEC.md §14 puts monitoring
/// on the operational surface, so a technician chasing an outage reads the register and the
/// device without needing an administrator — but which equipment is watched, how often, and
/// with what credential is configuration, and configuration is Admin's. That split is
/// deliberately tighter than the asset endpoints', where a technician records and issues
/// equipment: an asset is a fact about the world, and a monitored device is a decision
/// about what the system does.
/// </para>
/// <para>
/// <b>The poller does not use any of these routes.</b> <c>WP-3.2</c> pulls its
/// configuration from an endpoint of its own, authenticated with a service credential
/// scoped to it (ARCHITECTURE.md §7) — a machine-to-machine boundary, and the only place
/// the SNMP community string ever leaves the database. Keeping it off this group is what
/// lets every route here be answered without the secret.
/// </para>
/// <para>
/// <b>Three shapes of write, not one.</b> The edit corrects where a device is reached and
/// how it is polled. The monitoring switch is its own pair of routes because turning
/// monitoring off is an operational act with its own audit line. And the credential is its
/// own route because <c>PUT</c> is a full replacement, which applied to a write-only secret
/// would silently wipe one the form was never given. Each of the three is absent from the
/// others' request shapes, so the separation is structural rather than remembered.
/// </para>
/// </remarks>
internal static class DeviceEndpoints
{
    /// <summary>The route prefix these endpoints hang off.</summary>
    public const string RoutePrefix = "/api/v1/devices";

    /// <summary>Maps the monitored-device endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    public static void MapDevices(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix).WithTags("Monitoring");

        MapReads(group);
        MapWrites(group);
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        var reads = group.MapGroup(string.Empty).RequireAuthorization(ItmsPolicies.Technician);

        reads
            .MapGet("/", async (
                [AsParameters] ListDevicesQuery query,
                ListDevicesHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
                return result.ToOk();
            })
            .WithName("ListDevices")
            .WithSummary("Reads the monitored-device register, filtered, sorted, and paged.")
            .WithDescription(
                "Defaults to every device by asset tag, ascending. search matches the hostname and "
                + "the asset tag. The SNMP community string is never returned; snmpCredentialSet "
                + "says whether one is configured.")
            .Produces<PagedResult<DeviceResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        reads
            .MapGet("/{id:guid}", async (
                Guid id,
                GetDeviceHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return WithETag(result, context);
            })
            .WithName("GetDevice")
            .WithSummary("Reads one monitored device.")
            .WithDescription(
                "Carries an ETag naming the device's current version. Send it back as If-Match on a "
                + "write to be told the device has moved before the change is attempted. The SNMP "
                + "community string is never returned.")
            .Produces<DeviceResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapWrites(RouteGroupBuilder group)
    {
        var writes = group
            .MapGroup(string.Empty)
            .RequireAuthorization(ItmsPolicies.Admin)
            // Cookie auth plus a state-changing verb is exactly the shape CSRF exploits;
            // CONVENTIONS.md's security floor requires the check on every one of them.
            .AddEndpointFilter<AntiforgeryFilter>();

        writes
            .MapPost("/", async (
                RegisterDeviceRequest request,
                RegisterDeviceHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);

                if (result.IsFailure)
                {
                    return ProblemDetailsMapper.ToProblem(result.Error!);
                }

                SetETag(context, result.Value.Version);
                return MinimalApi.Created($"{RoutePrefix}/{result.Value.Response.Id}", result.Value.Response);
            })
            .WithValidation<RegisterDeviceRequest>()
            .WithName("RegisterDevice")
            .WithSummary("Starts monitoring an asset.")
            .WithDescription(
                "A monitored device is always an asset (invariant 6): assetId must name one that "
                + "already exists, and an asset has at most one device. The device needs a hostname "
                + "or an IP address. An snmpCommunity supplied here is stored and never returned by "
                + "any read; change it afterwards with PUT /api/v1/devices/{id}/snmp-credential.")
            .Produces<DeviceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        writes
            .MapPut("/{id:guid}", async (
                Guid id,
                UpdateDeviceRequest request,
                UpdateDeviceHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(id, request, EntityVersionTag.PreconditionFrom(context.Request), cancellationToken)
                    .ConfigureAwait(false);

                return WithETag(result, context);
            })
            .WithValidation<UpdateDeviceRequest>()
            .WithName("UpdateDevice")
            .WithSummary("Corrects where a device is reached and how it is polled.")
            .WithDescription(
                "A full replacement of the device's addressing and polling settings: a field left "
                + "out of the body is cleared, not left alone. The asset, the monitoring switch and "
                + "the SNMP community string are not part of this shape — the asset is fixed "
                + "(invariant 6) and the other two have routes of their own, so that a form which "
                + "never received the credential cannot wipe it. Send the device's ETag as If-Match "
                + "to be refused with 412 if it has moved since you read it.")
            .Produces<DeviceResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        MapMonitoringSwitch(
            writes,
            "/{id:guid}/enable",
            "EnableDeviceMonitoring",
            enabled: true,
            "Puts a device under the poller's watch.",
            "Already-watched devices are accepted and write nothing.");

        MapMonitoringSwitch(
            writes,
            "/{id:guid}/disable",
            "DisableDeviceMonitoring",
            enabled: false,
            "Takes a device off the poller's watch.",
            "It keeps its configuration and its history; it is simply no longer checked.");

        writes
            .MapPut("/{id:guid}/snmp-credential", async (
                Guid id,
                SetSnmpCredentialRequest request,
                SetSnmpCredentialHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .SetAsync(id, request, EntityVersionTag.PreconditionFrom(context.Request), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToNoContent();
            })
            .WithValidation<SetSnmpCredentialRequest>()
            .WithName("SetDeviceSnmpCredential")
            .WithSummary("Sets a device's read-only SNMP community string.")
            .WithDescription(
                "Write-only: no endpoint in this API ever returns the community string, the audit "
                + "trail records that it was set and not what it is, and reading the device back "
                + "answers snmpCredentialSet rather than the value. The read-only community is the "
                + "only kind this system accepts. Send the device's ETag as If-Match to be refused "
                + "with 412 if it has moved since you read it.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        writes
            .MapDelete("/{id:guid}/snmp-credential", async (
                Guid id,
                SetSnmpCredentialHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .ClearAsync(id, EntityVersionTag.PreconditionFrom(context.Request), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToNoContent();
            })
            .WithName("ClearDeviceSnmpCredential")
            .WithSummary("Removes a device's read-only SNMP community string.")
            .WithDescription(
                "Its own verb rather than an empty string on the write above, so that clearing a "
                + "credential and a client sending a blank field by mistake cannot be the same "
                + "request. A device with no credential is accepted and writes nothing.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
    }

    /// <summary>
    /// Maps one of the two monitoring-switch routes.
    /// </summary>
    /// <remarks>
    /// They differ in their route, their name, the boolean they pass, and their prose — and
    /// in nothing else. Written out twice, the second copy is where a missing
    /// <c>AntiforgeryFilter</c> or a forgotten 412 goes unnoticed, which is precisely the
    /// kind of omission CONVENTIONS.md's security floor cannot afford. This is the call
    /// <c>AssetEndpoints.MapLifecycle</c> made for the same reason.
    /// </remarks>
    private static void MapMonitoringSwitch(
        RouteGroupBuilder group,
        string route,
        string name,
        bool enabled,
        string summary,
        string description)
    {
        group
            .MapPost(route, async (
                Guid id,
                SetDeviceMonitoringHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(id, enabled, EntityVersionTag.PreconditionFrom(context.Request), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToNoContent();
            })
            .WithName(name)
            .WithSummary(summary)
            .WithDescription(
                description
                + " Send the device's ETag as If-Match to be refused with 412 if it has moved since "
                + "you read it.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
    }

    /// <summary>
    /// 200 with the device and its <c>ETag</c>, or the mapped problem response.
    /// </summary>
    /// <remarks>
    /// Not <c>ToOk</c>, because the header has to be set on the way out and only a success
    /// has a version to set it from. A failure goes through exactly the same mapper every
    /// other endpoint uses.
    /// </remarks>
    private static IResult WithETag(Result<DeviceDetail> result, HttpContext context)
    {
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblem(result.Error!);
        }

        SetETag(context, result.Value.Version);

        return MinimalApi.Ok(result.Value.Response);
    }

    private static void SetETag(HttpContext context, uint version) =>
        context.Response.Headers.ETag = EntityVersionTag.For(version);
}
