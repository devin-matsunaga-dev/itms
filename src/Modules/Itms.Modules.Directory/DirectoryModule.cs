using FluentValidation;
using Itms.Contracts.Lookups;
using Itms.Modules.Directory.Contracts;
using Itms.Modules.Directory.Features.Departments;
using Itms.Modules.Directory.Features.Departments.CreateDepartment;
using Itms.Modules.Directory.Features.Departments.GetDepartment;
using Itms.Modules.Directory.Features.Departments.ListDepartments;
using Itms.Modules.Directory.Features.Departments.SetDepartmentStatus;
using Itms.Modules.Directory.Features.Departments.UpdateDepartment;
using Itms.Modules.Directory.Features.Locations;
using Itms.Modules.Directory.Features.Locations.CreateLocation;
using Itms.Modules.Directory.Features.Locations.DeleteLocation;
using Itms.Modules.Directory.Features.Locations.GetLocation;
using Itms.Modules.Directory.Features.Locations.ListLocations;
using Itms.Modules.Directory.Features.Locations.MoveLocation;
using Itms.Modules.Directory.Features.Locations.UpdateLocation;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Itms.Modules.Directory;

/// <summary>
/// The Directory module's single registration and single mapping point
/// (ARCHITECTURE.md §3 rule 5).
/// </summary>
public static class DirectoryModule
{
    /// <summary>
    /// Registers persistence, this module's handlers and validators, and its public
    /// contract implementations.
    /// </summary>
    /// <param name="services">The container. <c>AddPlatform</c> and <c>AddMessaging</c> must already have run.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddDirectoryModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Built on the connection the ambient session hands out, never on a pool of its
        // own: that is what lets a handler write a directory change and its outbox event
        // in one transaction (STATUS.md, WP-0.4).
        services.AddDbContext<DirectoryDbContext>((provider, builder) =>
            builder.UseNpgsql(
                provider.GetRequiredService<IModuleDbSession>().Connection,
                npgsql => npgsql.MigrationsHistoryTable(
                    DirectoryDbContext.MigrationsHistoryTable,
                    DirectoryDbContext.SchemaName)));

        services.TryAddScoped<ListDepartmentsHandler>();
        services.TryAddScoped<GetDepartmentHandler>();
        services.TryAddScoped<CreateDepartmentHandler>();
        services.TryAddScoped<UpdateDepartmentHandler>();
        services.TryAddScoped<SetDepartmentStatusHandler>();

        services.TryAddScoped<ListLocationsHandler>();
        services.TryAddScoped<GetLocationHandler>();
        services.TryAddScoped<CreateLocationHandler>();
        services.TryAddScoped<UpdateLocationHandler>();
        services.TryAddScoped<MoveLocationHandler>();
        services.TryAddScoped<DeleteLocationHandler>();

        services.TryAddScoped<IValidator<CreateDepartmentRequest>, CreateDepartmentValidator>();
        services.TryAddScoped<IValidator<UpdateDepartmentRequest>, UpdateDepartmentValidator>();
        services.TryAddScoped<IValidator<CreateLocationRequest>, CreateLocationValidator>();
        services.TryAddScoped<IValidator<UpdateLocationRequest>, UpdateLocationValidator>();

        // The module's public contracts. Every other module reads departments and
        // locations through these and never sees the entities (ARCHITECTURE.md §3 rule 2).
        services.TryAddScoped<IDepartmentLookup, DepartmentLookupService>();
        services.TryAddScoped<ILocationLookup, LocationLookupService>();

        return services;
    }

    /// <summary>Maps the department and location endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    /// <returns>The route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapDirectoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapDepartments();
        endpoints.MapLocations();

        return endpoints;
    }

    /// <summary>
    /// Applies the directory migrations. The host calls this at startup in development;
    /// production applies migrations as a deliberate deployment step.
    /// </summary>
    /// <param name="services">A scoped service provider.</param>
    /// <param name="cancellationToken">Cancels the migration.</param>
    public static async Task MigrateDirectoryAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var context = services.GetRequiredService<DirectoryDbContext>();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
