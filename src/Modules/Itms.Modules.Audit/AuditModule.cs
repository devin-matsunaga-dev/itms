using Itms.Contracts.Auditing;
using Itms.Modules.Audit.Auditing;
using Itms.Modules.Audit.Persistence;
using Itms.Platform.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Itms.Modules.Audit;

/// <summary>
/// The Audit module's single registration and single mapping point
/// (ARCHITECTURE.md §3 rule 5).
/// </summary>
public static class AuditModule
{
    /// <summary>
    /// Registers persistence, the public <see cref="IAuditWriter"/>, and the consumer
    /// that audits every domain event.
    /// </summary>
    /// <param name="services">The container. <c>AddPlatform</c> and <c>AddMessaging</c> must already have run.</param>
    /// <returns>The container, for chaining.</returns>
    /// <remarks>
    /// The consumer itself is registered by <c>AddMessaging</c>'s assembly scan, not
    /// here, so this module's assembly has to be named in that call — otherwise the
    /// consumer is constructed by nothing and the trail is silently empty.
    /// </remarks>
    public static IServiceCollection AddAuditModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Built on the connection the ambient session hands out, never on a pool of its
        // own: that is what lets an audited change and the row recording it commit in one
        // transaction, and roll back together when the change fails.
        services.AddDbContext<AuditDbContext>((provider, builder) =>
            builder.UseNpgsql(
                provider.GetRequiredService<IModuleDbSession>().Connection,
                npgsql => npgsql.MigrationsHistoryTable(
                    AuditDbContext.MigrationsHistoryTable,
                    AuditDbContext.SchemaName)));

        services.TryAddScoped<AuditRecorder>();

        // The module's public contract. Every other module records a mutation through
        // this and never sees the table (ARCHITECTURE.md §3 rule 2).
        services.TryAddScoped<IAuditWriter, AuditWriter>();

        return services;
    }

    /// <summary>
    /// Maps the audit endpoints — there are none. The trail is written in Phase 0 and
    /// read in <c>WP-5.9 — Audit log viewer</c>; a module with no read surface yet is
    /// deliberate, and this exists so the composition root treats every module alike.
    /// </summary>
    /// <param name="endpoints">The host's route builder.</param>
    /// <returns>The route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints;
    }

    /// <summary>
    /// Applies the audit migrations. The host calls this at startup in development;
    /// production applies migrations as a deliberate deployment step.
    /// </summary>
    /// <param name="services">A scoped service provider.</param>
    /// <param name="cancellationToken">Cancels the migration.</param>
    public static async Task MigrateAuditAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var context = services.GetRequiredService<AuditDbContext>();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
