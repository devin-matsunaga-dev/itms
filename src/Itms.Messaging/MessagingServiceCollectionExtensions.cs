using System.Reflection;
using Itms.Messaging.Abstractions;
using Itms.Messaging.Outbox;
using Itms.Messaging.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Itms.Messaging;

/// <summary>
/// Registers the in-process bus. The host calls this once, after <c>AddPlatform</c> and
/// before any <c>AddXxxModule</c>, because modules take <see cref="IEventPublisher"/>
/// from here.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>Adds the outbox, the publisher, and the background dispatcher.</summary>
    /// <param name="services">The container.</param>
    /// <param name="configuration">Bound to <see cref="MessagingOptions.SectionName"/> when present.</param>
    /// <param name="consumerAssemblies">
    /// The assemblies scanned for <see cref="IEventConsumer{TEvent}"/> implementations —
    /// the module assemblies. Messaging cannot reference a module, so the host names them.
    /// </param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] consumerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(consumerAssemblies);

        services
            .AddOptions<MessagingOptions>()
            .Bind(configuration.GetSection(MessagingOptions.SectionName))
            // Checked at startup rather than on first use, so a bad appsettings value
            // fails the deployment instead of the first event of the day.
            .Validate(o => o.BatchSize is >= 1 and <= 500, "Messaging:BatchSize must be between 1 and 500.")
            .Validate(o => o.MaxAttempts is >= 1 and <= 100, "Messaging:MaxAttempts must be between 1 and 100.")
            .Validate(o => o.PollInterval > TimeSpan.Zero, "Messaging:PollInterval must be positive.")
            .Validate(o => o.BaseRetryDelay > TimeSpan.Zero, "Messaging:BaseRetryDelay must be positive.")
            .Validate(o => o.MaxRetryDelay >= o.BaseRetryDelay, "Messaging:MaxRetryDelay must be at least BaseRetryDelay.")
            .Validate(o => o.LeaseDuration > TimeSpan.Zero, "Messaging:LeaseDuration must be positive.")
            .ValidateOnStart();

        AddMessagingCore(services, consumerAssemblies);
        services.AddHostedService<OutboxDispatcher>();

        return services;
    }

    /// <summary>
    /// Adds everything except the background loop. Integration tests use this so they can
    /// drive <see cref="IOutboxProcessor"/> one pass at a time instead of racing a timer.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <param name="consumerAssemblies">The assemblies scanned for consumers.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddMessagingCore(
        this IServiceCollection services,
        params Assembly[] consumerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(consumerAssemblies);

        // Fully qualified on purpose: this file's own namespace also declares an
        // AssemblyMarker, and an unqualified name here silently scans the wrong assembly
        // for events — leaving every message undeliverable as an unknown type.
        services.TryAddSingleton(_ => new DomainEventTypeRegistry([typeof(Contracts.AssemblyMarker).Assembly]));
        services.TryAddSingleton<DomainEventSerializer>();

        var registrations = EventConsumerRegistry.Discover(consumerAssemblies);
        var registry = new EventConsumerRegistry(registrations);
        services.TryAddSingleton(registry);

        foreach (var consumerType in registry.ConsumerTypes)
        {
            // Registered as the concrete type rather than as IEventConsumer<T>: the
            // dispatcher resolves the exact class named in the consumption row, and a
            // class handling two events must not be resolved twice.
            services.TryAddScoped(consumerType);
        }

        // One connection per scope, shared by every context in it. This is what makes a
        // module's SaveChanges and the outbox write one transaction rather than two.
        services.TryAddScoped<DbSession>();
        services.TryAddScoped<IDbSession>(sp => sp.GetRequiredService<DbSession>());

        services.AddDbContext<OutboxDbContext>((sp, builder) =>
            builder.UseNpgsql(
                sp.GetRequiredService<IDbSession>().Connection,
                npgsql => npgsql.MigrationsHistoryTable(
                    OutboxDbContext.MigrationsHistoryTable,
                    OutboxDbContext.SchemaName)));

        services.TryAddScoped<IEventPublisher, OutboxPublisher>();
        services.TryAddScoped<IOutboxProcessor, OutboxProcessor>();

        return services;
    }

    /// <summary>
    /// Applies the messaging migrations. The host calls this at startup in development;
    /// production applies migrations as a deliberate deployment step.
    /// </summary>
    /// <param name="services">A scoped service provider.</param>
    /// <param name="cancellationToken">Cancels the migration.</param>
    public static async Task MigrateMessagingAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var context = services.GetRequiredService<OutboxDbContext>();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
