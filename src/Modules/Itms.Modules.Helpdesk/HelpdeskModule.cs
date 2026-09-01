using FluentValidation;
using Itms.Contracts.Lookups;
using Itms.Modules.Helpdesk.Configuration;
using Itms.Modules.Helpdesk.Contracts;
using Itms.Modules.Helpdesk.Features.TicketAttachments;
using Itms.Modules.Helpdesk.Features.TicketAttachments.DownloadTicketAttachment;
using Itms.Modules.Helpdesk.Features.TicketAttachments.ListTicketAttachments;
using Itms.Modules.Helpdesk.Features.TicketAttachments.UploadTicketAttachment;
using Itms.Modules.Helpdesk.Features.TicketCategories;
using Itms.Modules.Helpdesk.Features.TicketCategories.CreateTicketCategory;
using Itms.Modules.Helpdesk.Features.TicketCategories.GetTicketCategory;
using Itms.Modules.Helpdesk.Features.TicketCategories.ListTicketCategories;
using Itms.Modules.Helpdesk.Features.TicketCategories.SetTicketCategoryStatus;
using Itms.Modules.Helpdesk.Features.TicketCategories.UpdateTicketCategory;
using Itms.Modules.Helpdesk.Features.TicketComments;
using Itms.Modules.Helpdesk.Features.TicketComments.AddTicketComment;
using Itms.Modules.Helpdesk.Features.TicketComments.ListTicketComments;
using Itms.Modules.Helpdesk.Features.TicketHistory;
using Itms.Modules.Helpdesk.Features.TicketHistory.ListTicketHistory;
using Itms.Modules.Helpdesk.Features.TicketPriorities;
using Itms.Modules.Helpdesk.Features.TicketPriorities.CreateTicketPriority;
using Itms.Modules.Helpdesk.Features.TicketPriorities.GetTicketPriority;
using Itms.Modules.Helpdesk.Features.TicketPriorities.ListTicketPriorities;
using Itms.Modules.Helpdesk.Features.TicketPriorities.SetTicketPriorityStatus;
using Itms.Modules.Helpdesk.Features.TicketPriorities.UpdateTicketPriority;
using Itms.Modules.Helpdesk.Features.Tickets;
using Itms.Modules.Helpdesk.Features.Tickets.AssignTicket;
using Itms.Modules.Helpdesk.Features.Tickets.ChangeTicketStatus;
using Itms.Modules.Helpdesk.Features.Tickets.CreateTicket;
using Itms.Modules.Helpdesk.Features.Tickets.GetTicket;
using Itms.Modules.Helpdesk.Features.Tickets.ListTickets;
using Itms.Modules.Helpdesk.Features.Tickets.TicketCounters;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
    /// <para>
    /// No public contract implementation, and no event consumer. This module <em>publishes</em>
    /// from WP-1.5 — <c>CreateTicketHandler</c> raises <c>TicketCreated</c> — but publishing
    /// needs nothing registered here: <c>IEventPublisher</c> comes from <c>AddMessaging</c>,
    /// which the composition root has already run.
    /// </para>
    /// <para>
    /// <b>A consumer would be different.</b> The assembly list passed to <c>AddMessaging</c> is
    /// what the bus scans for <c>IEventConsumer&lt;T&gt;</c>, and Helpdesk is not on it. The
    /// first package to add a consumer here — the one that refreshes the cached requester and
    /// department names when Identity and Directory start announcing renames — must add
    /// <c>Itms.Modules.Helpdesk</c> to that call, or the consumer silently never runs and no
    /// test would notice.
    /// </para>
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

        AddAttachmentStorage(services);

        services.TryAddScoped<ListTicketCategoriesHandler>();
        services.TryAddScoped<GetTicketCategoryHandler>();
        services.TryAddScoped<CreateTicketCategoryHandler>();
        services.TryAddScoped<UpdateTicketCategoryHandler>();
        services.TryAddScoped<SetTicketCategoryStatusHandler>();

        services.TryAddScoped<CreateTicketHandler>();
        services.TryAddScoped<ListTicketsHandler>();
        services.TryAddScoped<TicketCountersHandler>();
        services.TryAddScoped<GetTicketHandler>();
        services.TryAddScoped<ChangeTicketStatusHandler>();
        services.TryAddScoped<AssignTicketHandler>();
        services.TryAddScoped<ListTicketHistoryHandler>();
        services.TryAddScoped<AddTicketCommentHandler>();
        services.TryAddScoped<ListTicketCommentsHandler>();
        services.TryAddScoped<UploadTicketAttachmentHandler>();
        services.TryAddScoped<ListTicketAttachmentsHandler>();
        services.TryAddScoped<DownloadTicketAttachmentHandler>();

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
        services.TryAddScoped<IValidator<CreateTicketRequest>, CreateTicketValidator>();
        services.TryAddScoped<IValidator<AssignTicketRequest>, AssignTicketValidator>();
        services.TryAddScoped<IValidator<AddTicketCommentRequest>, AddTicketCommentValidator>();

        // This module's reference count for the directory screens: how many tickets are
        // filed against a department
        // (WP-2.4). TryAddEnumerable rather than TryAddScoped, because every module that
        // holds such a reference registers one and Directory reads all of them.
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IDirectoryUsageLookup, TicketDirectoryUsageLookup>());

        return services;
    }

    /// <summary>
    /// Binds and validates the attachment settings, and registers the store the bytes go
    /// through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Validated at startup, not on the first upload.</b> A deployment with no storage
    /// root, a nonsensical cap, or an extension nothing knows how to check should fail to
    /// come up — the alternative is a service that looks healthy until somebody attaches a
    /// screenshot to a ticket, which is the worst moment to discover it.
    /// </para>
    /// <para>
    /// <b>The allowlist can only narrow.</b> An extension that
    /// <see cref="AttachmentContentRules"/> has no signature rule for is rejected here,
    /// because accepting it would mean accepting a file type the content check can only
    /// answer "no" for — an allowlist entry that silently refuses every upload, or worse, a
    /// future edit that makes it silently accept anything.
    /// </para>
    /// </remarks>
    /// <param name="services">The container.</param>
    private static void AddAttachmentStorage(IServiceCollection services)
    {
        services
            .AddOptions<HelpdeskAttachmentOptions>()
            .BindConfiguration(HelpdeskAttachmentOptions.SectionName)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.RootPath),
                $"{HelpdeskAttachmentOptions.SectionName}:RootPath must name a directory outside the web root.")
            .Validate(
                options => options.MaxBytes > 0,
                $"{HelpdeskAttachmentOptions.SectionName}:MaxBytes must be greater than zero.")
            .Validate(
                options => options.AllowedExtensions.Count > 0,
                $"{HelpdeskAttachmentOptions.SectionName}:AllowedExtensions must name at least one extension.")
            .Validate(
                options => options.AllowedExtensions.All(extension =>
                    AttachmentContentRules.KnownExtensions.Contains(
                        extension.ToLowerInvariant(),
                        StringComparer.Ordinal)),
                $"{HelpdeskAttachmentOptions.SectionName}:AllowedExtensions may only narrow the built-in set: "
                + string.Join(", ", AttachmentContentRules.KnownExtensions))
            .ValidateOnStart();

        // Singleton, built here rather than by the container's constructor injection,
        // because resolving a configured relative path against the content root is a
        // composition concern and the store is better off not knowing what a host is.
        services.TryAddSingleton<IAttachmentStore>(provider => new FileSystemAttachmentStore(
            Path.GetFullPath(
                provider.GetRequiredService<IOptions<HelpdeskAttachmentOptions>>().Value.RootPath,
                provider.GetRequiredService<IHostEnvironment>().ContentRootPath)));
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
        endpoints.MapTicketComments();
        endpoints.MapTicketAttachments();

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
