using FluentValidation;
using Itms.Modules.Monitoring.Features.Devices;
using Itms.Modules.Monitoring.Features.Devices.GetDevice;
using Itms.Modules.Monitoring.Features.Devices.ListDevices;
using Itms.Modules.Monitoring.Features.Devices.RegisterDevice;
using Itms.Modules.Monitoring.Features.Devices.SetDeviceMonitoring;
using Itms.Modules.Monitoring.Features.Devices.SetSnmpCredential;
using Itms.Modules.Monitoring.Features.Devices.UpdateDevice;
using Itms.Modules.Monitoring.Persistence;
using Itms.Platform.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Itms.Modules.Monitoring;

/// <summary>
/// The Monitoring module's single registration and single mapping point
/// (ARCHITECTURE.md §3 rule 5).
/// </summary>
public static class MonitoringModule
{
    /// <summary>
    /// Registers persistence and this module's handlers and validators.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This module publishes nothing and consumes nothing, yet.</b> ARCHITECTURE.md §5
    /// names <c>DeviceWentOffline</c> and <c>DeviceRecovered</c> as Monitoring's two events
    /// and both are state transitions caused by check results — so <c>WP-3.3</c>, which
    /// owns ingestion and the state machine, is the package that starts publishing them.
    /// Publishing needs nothing registered here: <c>IEventPublisher</c> comes from
    /// <c>AddMessaging</c>, which the composition root has already run. <b>A consumer
    /// would</b>: the assembly list passed to <c>AddMessaging</c> is what the bus scans for
    /// <c>IEventConsumer&lt;T&gt;</c>, and Monitoring is not on it. The first package to add
    /// one here must add <c>Itms.Modules.Monitoring</c> to that call, or the consumer
    /// silently never runs and no test would notice.
    /// </para>
    /// <para>
    /// <b>This module implements no public contract.</b> <c>IDeviceLookup</c> is still
    /// deliberately unwritten — WP-0.3 settled that a lookup contract is written by the
    /// package that first needs one, because speculative contracts rot, and nothing outside
    /// Monitoring reads a device yet. <c>WP-3.6</c>'s alerts are the likely first consumer.
    /// </para>
    /// <para>
    /// It does <em>consume</em> one: <c>IAssetLookup</c> is how a device is proved to be an
    /// asset (invariant 6), and it is the only route by which this module learns anything
    /// about equipment. Monitoring references neither <c>Modules.Assets</c> nor the assets
    /// schema, and <c>ModuleBoundaryTests</c> asserts it.
    /// </para>
    /// </remarks>
    /// <param name="services">The container. <c>AddPlatform</c> and <c>AddMessaging</c> must already have run.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddMonitoringModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Built on the connection the ambient session hands out, never on a pool of its
        // own: that is what lets a handler write a device change and its outbox event in
        // one transaction (STATUS.md, WP-0.4).
        services.AddDbContext<MonitoringDbContext>((provider, builder) =>
            builder.UseNpgsql(
                provider.GetRequiredService<IModuleDbSession>().Connection,
                npgsql => npgsql.MigrationsHistoryTable(
                    MonitoringDbContext.MigrationsHistoryTable,
                    MonitoringDbContext.SchemaName)));

        services.TryAddScoped<ListDevicesHandler>();
        services.TryAddScoped<GetDeviceHandler>();
        services.TryAddScoped<RegisterDeviceHandler>();
        services.TryAddScoped<UpdateDeviceHandler>();
        services.TryAddScoped<SetDeviceMonitoringHandler>();
        services.TryAddScoped<SetSnmpCredentialHandler>();

        services.TryAddScoped<IValidator<RegisterDeviceRequest>, RegisterDeviceValidator>();
        services.TryAddScoped<IValidator<UpdateDeviceRequest>, UpdateDeviceValidator>();
        services.TryAddScoped<IValidator<SetSnmpCredentialRequest>, SetSnmpCredentialValidator>();

        return services;
    }

    /// <summary>Maps the monitored-device endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    /// <returns>The route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapMonitoringEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapDevices();

        return endpoints;
    }

    /// <summary>
    /// Applies the monitoring migrations.
    /// </summary>
    /// <remarks>
    /// The host calls this at startup in development; production applies migrations as a
    /// deliberate deployment step. This module seeds nothing — it has no reference data,
    /// so it does not join the first-run seeder gap WP-6.6 owns.
    /// </remarks>
    /// <param name="services">A scoped service provider.</param>
    /// <param name="cancellationToken">Cancels the migration.</param>
    public static async Task MigrateMonitoringAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var context = services.GetRequiredService<MonitoringDbContext>();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
