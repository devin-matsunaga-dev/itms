using FluentValidation;
using Itms.Contracts.Lookups;
using Itms.Modules.Assets.Contracts;
using Itms.Modules.Assets.Features.AssetStatuses;
using Itms.Modules.Assets.Features.AssetStatuses.CreateAssetStatus;
using Itms.Modules.Assets.Features.AssetStatuses.GetAssetStatus;
using Itms.Modules.Assets.Features.AssetStatuses.ListAssetStatuses;
using Itms.Modules.Assets.Features.AssetStatuses.SetAssetStatusActivation;
using Itms.Modules.Assets.Features.AssetStatuses.UpdateAssetStatus;
using Itms.Modules.Assets.Features.AssetTypes;
using Itms.Modules.Assets.Features.AssetTypes.CreateAssetType;
using Itms.Modules.Assets.Features.AssetTypes.GetAssetType;
using Itms.Modules.Assets.Features.AssetTypes.ListAssetTypes;
using Itms.Modules.Assets.Features.AssetTypes.SetAssetTypeActivation;
using Itms.Modules.Assets.Features.AssetTypes.UpdateAssetType;
using Itms.Modules.Assets.Features.AssetHistory;
using Itms.Modules.Assets.Features.AssetHistory.ListAssetHistory;
using Itms.Modules.Assets.Features.Assets;
using Itms.Modules.Assets.Features.Assets.AssignAsset;
using Itms.Modules.Assets.Features.Assets.CreateAsset;
using Itms.Modules.Assets.Features.Assets.GetAsset;
using Itms.Modules.Assets.Features.Assets.ListAssets;
using Itms.Modules.Assets.Features.Assets.ListAssetTickets;
using Itms.Modules.Assets.Features.Assets.RetireAsset;
using Itms.Modules.Assets.Features.Assets.ReturnAssetToService;
using Itms.Modules.Assets.Features.Assets.SendAssetForRepair;
using Itms.Modules.Assets.Features.Assets.UpdateAsset;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Itms.Modules.Assets;

/// <summary>
/// The Assets module's single registration and single mapping point
/// (ARCHITECTURE.md §3 rule 5).
/// </summary>
public static class AssetsModule
{
    /// <summary>
    /// Registers persistence and this module's handlers and validators.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The public contract is implemented from WP-2.5.</b> <c>IAssetLookup</c> is how
    /// every other module reads assets, and <c>AssetLookupService</c> is the answer —
    /// Helpdesk's ticket link and its detail read are its first consumers. It was the last
    /// of the four lookup contracts to get one.
    /// </para>
    /// <para>
    /// <b>This module publishes two events and consumes none.</b>
    /// <c>AssetLifecycleMutation</c> raises <c>AssetAssigned</c> and
    /// <c>AssetStatusChanged</c> (ARCHITECTURE.md §5), and publishing needs nothing
    /// registered here — <c>IEventPublisher</c> comes from <c>AddMessaging</c>, which the
    /// composition root has already run. <b>A consumer would</b>: the assembly list passed
    /// to <c>AddMessaging</c> is what the bus scans for <c>IEventConsumer&lt;T&gt;</c>, and
    /// Assets is still not on it. The first package to add one here — the department rename
    /// and location move that refresh an asset's cached names are the obvious candidates —
    /// must add <c>Itms.Modules.Assets</c> to that call, or the consumer silently never runs
    /// and no test would notice.
    /// </para>
    /// </remarks>
    /// <param name="services">The container. <c>AddPlatform</c> and <c>AddMessaging</c> must already have run.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddAssetsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Built on the connection the ambient session hands out, never on a pool of its
        // own: that is what lets a handler write an asset change and its outbox event in
        // one transaction (STATUS.md, WP-0.4).
        services.AddDbContext<AssetsDbContext>((provider, builder) =>
            builder.UseNpgsql(
                provider.GetRequiredService<IModuleDbSession>().Connection,
                npgsql => npgsql.MigrationsHistoryTable(
                    AssetsDbContext.MigrationsHistoryTable,
                    AssetsDbContext.SchemaName)));

        services.TryAddScoped<ListAssetTypesHandler>();
        services.TryAddScoped<GetAssetTypeHandler>();
        services.TryAddScoped<CreateAssetTypeHandler>();
        services.TryAddScoped<UpdateAssetTypeHandler>();
        services.TryAddScoped<SetAssetTypeActivationHandler>();

        services.TryAddScoped<ListAssetStatusesHandler>();
        services.TryAddScoped<GetAssetStatusHandler>();
        services.TryAddScoped<CreateAssetStatusHandler>();
        services.TryAddScoped<UpdateAssetStatusHandler>();
        services.TryAddScoped<SetAssetStatusActivationHandler>();

        services.TryAddScoped<CreateAssetHandler>();
        services.TryAddScoped<UpdateAssetHandler>();
        services.TryAddScoped<GetAssetHandler>();
        services.TryAddScoped<ListAssetsHandler>();
        services.TryAddScoped<ListAssetHistoryHandler>();
        services.TryAddScoped<ListAssetTicketsHandler>();

        // The timeline writer and the transaction envelope every lifecycle operation shares.
        // Scoped, because both hold the request's own DbContext and the transaction it is
        // enlisted in.
        services.TryAddScoped<AssetHistoryRecorder>();
        services.TryAddScoped<AssetLifecycleMutation>();

        services.TryAddScoped<AssignAssetHandler>();
        services.TryAddScoped<SendAssetForRepairHandler>();
        services.TryAddScoped<ReturnAssetToServiceHandler>();
        services.TryAddScoped<RetireAssetHandler>();

        services.TryAddScoped<IValidator<CreateAssetTypeRequest>, CreateAssetTypeValidator>();
        services.TryAddScoped<IValidator<UpdateAssetTypeRequest>, UpdateAssetTypeValidator>();
        services.TryAddScoped<IValidator<CreateAssetStatusRequest>, CreateAssetStatusValidator>();
        services.TryAddScoped<IValidator<UpdateAssetStatusRequest>, UpdateAssetStatusValidator>();
        services.TryAddScoped<IValidator<CreateAssetRequest>, CreateAssetValidator>();
        services.TryAddScoped<IValidator<UpdateAssetRequest>, UpdateAssetValidator>();
        services.TryAddScoped<IValidator<AssignAssetRequest>, AssignAssetValidator>();
        services.TryAddScoped<IValidator<AssetLifecycleRequest>, AssetLifecycleRequestValidator>();

        // This module's public contract: how Helpdesk, Monitoring, and Alerts read an asset
        // without referencing Assets (ARCHITECTURE.md §3 rule 2). Scoped, because it holds
        // the request's own context.
        services.TryAddScoped<IAssetLookup, AssetLookupService>();

        // This module's reference count for the directory screens: how much equipment
        // sits in a department or a room
        // (WP-2.4). TryAddEnumerable rather than TryAddScoped, because every module that
        // holds such a reference registers one and Directory reads all of them.
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IDirectoryUsageLookup, AssetDirectoryUsageLookup>());

        return services;
    }

    /// <summary>Maps the asset, asset-type, and asset-status endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    /// <returns>The route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapAssetsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapAssetTypes();
        endpoints.MapAssetStatuses();
        endpoints.MapAssets();

        return endpoints;
    }

    /// <summary>
    /// Applies the assets migrations.
    /// </summary>
    /// <remarks>
    /// The host calls this at startup in development; production applies migrations as a
    /// deliberate deployment step. The reference data does not travel with them — see
    /// <c>AssetsReferenceDataSeeder</c>, which the same deployment step must also run.
    /// </remarks>
    /// <param name="services">A scoped service provider.</param>
    /// <param name="cancellationToken">Cancels the migration.</param>
    public static async Task MigrateAssetsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var context = services.GetRequiredService<AssetsDbContext>();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
