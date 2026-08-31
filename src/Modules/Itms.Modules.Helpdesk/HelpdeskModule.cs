using FluentValidation;
using Itms.Modules.Helpdesk.Features.TicketCategories;
using Itms.Modules.Helpdesk.Features.TicketCategories.CreateTicketCategory;
using Itms.Modules.Helpdesk.Features.TicketCategories.GetTicketCategory;
using Itms.Modules.Helpdesk.Features.TicketCategories.ListTicketCategories;
using Itms.Modules.Helpdesk.Features.TicketCategories.SetTicketCategoryStatus;
using Itms.Modules.Helpdesk.Features.TicketCategories.UpdateTicketCategory;
using Itms.Modules.Helpdesk.Features.TicketHistory;
using Itms.Modules.Helpdesk.Features.TicketHistory.ListTicketHistory;
using Itms.Modules.Helpdesk.Features.TicketPriorities;
using Itms.Modules.Helpdesk.Features.TicketPriorities.CreateTicketPriority;
using Itms.Modules.Helpdesk.Features.TicketPriorities.GetTicketPriority;
using Itms.Modules.Helpdesk.Features.TicketPriorities.ListTicketPriorities;
using Itms.Modules.Helpdesk.Features.TicketPriorities.SetTicketPriorityStatus;
using Itms.Modules.Helpdesk.Features.TicketPriorities.UpdateTicketPriority;
using Itms.Modules.Helpdesk.Features.Tickets;
using Itms.Modules.Helpdesk.Features.Tickets.ChangeTicketStatus;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Itms.Modules.Helpdesk;

/// <summary>
/// The Helpdesk module's single registration and single mapping point
/// (ARCHITECTURE.md §3 rule 5).
/// </summary>
public static class HelpdeskModule
{
    /// <summary>
    /// Registers persistence and this module's handlers and validators.
    /// </summary>
    /// <remarks>
    /// No public contract implementation yet, and no event consumer: nothing outside
    /// Helpdesk reads a category or a priority, and ARCHITECTURE.md §5 names no event
    /// either of them raises. When tickets arrive and the module starts publishing, the
    /// composition root's <c>AddMessaging</c> call is what has to learn about it.
    /// </remarks>
    /// <param name="services">The container. <c>AddPlatform</c> and <c>AddMessaging</c> must already have run.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddHelpdeskModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Built on the connection the ambient session hands out, never on a pool of its
        // own: that is what lets a handler write a helpdesk change and its outbox event
        // in one transaction (STATUS.md, WP-0.4).
        services.AddDbContext<HelpdeskDbContext>((provider, builder) =>
            builder.UseNpgsql(
                provider.GetRequiredService<IModuleDbSession>().Connection,
                npgsql => npgsql.MigrationsHistoryTable(
                    HelpdeskDbContext.MigrationsHistoryTable,
                    HelpdeskDbContext.SchemaName)));

        // Scoped, because it claims its number on the scope's own connection inside the
        // scope's own transaction.
        services.TryAddScoped<TicketNumberGenerator>();

        services.TryAddScoped<ListTicketCategoriesHandler>();
        services.TryAddScoped<GetTicketCategoryHandler>();
        services.TryAddScoped<CreateTicketCategoryHandler>();
        services.TryAddScoped<UpdateTicketCategoryHandler>();
        services.TryAddScoped<SetTicketCategoryStatusHandler>();

        services.TryAddScoped<ChangeTicketStatusHandler>();
        services.TryAddScoped<ListTicketHistoryHandler>();

        // Scoped, because it adds its entries to the scope's own context and they go to
        // the database on that scope's own SaveChanges, inside that scope's transaction.
        services.TryAddScoped<TicketHistoryRecorder>();

        services.TryAddScoped<ListTicketPrioritiesHandler>();
        services.TryAddScoped<GetTicketPriorityHandler>();
        services.TryAddScoped<CreateTicketPriorityHandler>();
        services.TryAddScoped<UpdateTicketPriorityHandler>();
        services.TryAddScoped<SetTicketPriorityStatusHandler>();

        services.TryAddScoped<IValidator<CreateTicketCategoryRequest>, CreateTicketCategoryValidator>();
        services.TryAddScoped<IValidator<UpdateTicketCategoryRequest>, UpdateTicketCategoryValidator>();
        services.TryAddScoped<IValidator<CreateTicketPriorityRequest>, CreateTicketPriorityValidator>();
        services.TryAddScoped<IValidator<UpdateTicketPriorityRequest>, UpdateTicketPriorityValidator>();
        services.TryAddScoped<IValidator<ChangeTicketStatusRequest>, ChangeTicketStatusValidator>();

        return services;
    }

    /// <summary>Maps the ticket, ticket-category, and ticket-priority endpoints.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    /// <returns>The route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapHelpdeskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapTicketCategories();
        endpoints.MapTicketPriorities();
        endpoints.MapTickets();

        return endpoints;
    }

    /// <summary>
    /// Applies the helpdesk migrations.
    /// </summary>
    /// <remarks>
    /// The host calls this at startup in development; production applies migrations as a
    /// deliberate deployment step. The reference data does not travel with them — see
    /// <c>HelpdeskReferenceDataSeeder</c>, which the same deployment step must also run.
    /// </remarks>
    /// <param name="services">A scoped service provider.</param>
    /// <param name="cancellationToken">Cancels the migration.</param>
    public static async Task MigrateHelpdeskAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var context = services.GetRequiredService<HelpdeskDbContext>();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
