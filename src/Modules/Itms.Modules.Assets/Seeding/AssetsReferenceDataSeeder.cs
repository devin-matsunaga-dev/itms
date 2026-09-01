using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Itms.Modules.Assets.Seeding;

/// <summary>
/// Puts the asset types and statuses SPEC.md §3 names into an empty database.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every environment.</b> A type and a status are reference data, not business data: a
/// deployment with no statuses could not record an asset at all. This is the same reason
/// Identity seeds its roles and Helpdesk its priorities everywhere.
/// </para>
/// <para>
/// <b>A seeder rather than a migration, for the reason WP-1.1 gives.</b> Reference data
/// inserted by a migration cannot be restored once removed, and the integration suite
/// removes it on every test — CONVENTIONS.md fixes the between-test reset as a Respawn
/// truncate, and a truncate empties a migration-seeded table with nothing able to put the
/// rows back.
/// </para>
/// <para>
/// <b>THIS WIDENS THE DEPLOYMENT GAP RECORDED AGAINST WP-6.6.</b> The host calls this
/// inside the same Development-only startup block as the other seeders, so a production
/// deployment that applies migrations and stops has no asset types and no asset statuses
/// and cannot record an asset — exactly as it would have no ticket priorities and could not
/// accept a ticket. <c>WP-6.6 — Deployment &amp; runbook</c> owns first-run setup and must
/// run this alongside <c>HelpdeskReferenceDataSeeder</c> and Identity's roles. It is
/// deliberately the <em>same</em> gap rather than a second mechanism: one deployment step
/// that runs every reference-data seeder is what WP-6.6 has to build, and inventing a
/// separate strategy here would give it two problems instead of one.
/// </para>
/// <para>
/// Idempotent <em>by id</em>, not by name: the ids are literals, so re-running this finds
/// the same rows even after an administrator has renamed one, and a rename therefore
/// survives a restart. It never reactivates a retired row and never edits an existing one —
/// an operator's decisions outrank the seed.
/// </para>
/// </remarks>
public static class AssetsReferenceDataSeeder
{
    /// <summary>The twelve types SPEC.md §3 names, in the order it names them.</summary>
    private static readonly (Guid Id, string Name, string Description, int SortOrder)[] Types =
    [
        (new("01a06310-0100-7a2e-9f41-2c7d5b9e4a01"), "Desktop", "Fixed workstations.", 10),
        (new("01a06310-0101-7b3f-8e52-3d8e6caf5b12"), "Laptop", "Portable workstations.", 20),
        (new("01a06310-0102-7c40-9f63-4e9f7db06c23"), "Server", "Physical and virtual server hosts.", 30),
        (new("01a06310-0103-7d51-8074-5fa08ec17d34"), "Switch", "Network switches.", 40),
        (new("01a06310-0104-7e62-9185-60b19fd28e45"), "Router", "Routers and gateways.", 50),
        (new("01a06310-0105-7f73-8296-71c2a0e39f56"), "Firewall", "Perimeter and internal firewalls.", 60),
        (new("01a06310-0106-7084-93a7-82d3b1f4a067"), "Access Point", "Wireless access points.", 70),
        (new("01a06310-0107-7195-84b8-93e4c205b178"), "Printer", "Printers, scanners, and multifunction devices.", 80),
        (new("01a06310-0108-72a6-95c9-a4f5d316c289"), "Phone", "Desk phones and mobile handsets.", 90),
        (new("01a06310-0109-73b7-86da-b506e427d39a"), "Tablet", "Tablets and convertible devices.", 100),
        (new("01a06310-010a-74c8-97eb-c617f538e4ab"), "UPS", "Uninterruptible power supplies.", 110),
        (new("01a06310-010b-75d9-88fc-d728069f05bc"), "Other", "Anything that does not fit the types above.", 120),
    ];

    // sort_order in tens, so a thirteenth type can be slotted between two existing ones
    // without renumbering the list.

    /// <summary>The six statuses SPEC.md §3 names, in lifecycle order.</summary>
    private static readonly (Guid Id, string Code, string Name, string Description, int SortOrder)[] Statuses =
    [
        (new("01a06310-0200-7a1b-9c2d-3e4f5061728a"), AssetStatusCode.InStock, "In Stock", "Held and not yet issued to anybody.", 10),
        (new("01a06310-0201-7b2c-8d3e-4f5061728b9c"), AssetStatusCode.Deployed, "Deployed", "Issued and in service.", 20),
        (new("01a06310-0202-7c3d-9e4f-5061728b9cad"), AssetStatusCode.Repair, "Repair", "Away being fixed.", 30),
        (new("01a06310-0203-7d4e-8f50-61728b9cadbe"), AssetStatusCode.Retired, "Retired", "Taken out of service and kept on the books.", 40),
        (new("01a06310-0204-7e5f-9061-728b9cadbecf"), AssetStatusCode.Lost, "Lost", "Unaccounted for.", 50),
        (new("01a06310-0205-7f60-8172-8b9cadbecfd0"), AssetStatusCode.Disposed, "Disposed", "Physically gone — scrapped, sold, or destroyed.", 60),
    ];

    /// <summary>The ids of the seeded types, so a caller can name one without a lookup.</summary>
    public static IReadOnlyList<Guid> TypeIds { get; } = [.. Types.Select(type => type.Id)];

    /// <summary>The ids of the seeded statuses, in lifecycle order.</summary>
    public static IReadOnlyList<Guid> StatusIds { get; } = [.. Statuses.Select(status => status.Id)];

    /// <summary>Seeds anything missing. Safe to run on every startup.</summary>
    /// <param name="services">A scoped service provider.</param>
    /// <param name="cancellationToken">Cancels the seeding.</param>
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var database = services.GetRequiredService<AssetsDbContext>();
        var clock = services.GetRequiredService<IClock>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AssetsReferenceDataSeeder));
        var now = clock.UtcNow;

        var types = await SeedTypesAsync(database, now, cancellationToken).ConfigureAwait(false);
        var statuses = await SeedStatusesAsync(database, now, cancellationToken).ConfigureAwait(false);

        if (types > 0 || statuses > 0)
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            AssetsLog.SeededReferenceData(logger, types, statuses);
        }
    }

    private static async Task<int> SeedTypesAsync(
        AssetsDbContext database,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await database.AssetTypes
            .Select(type => type.Id)
            .ToHashSetAsync(cancellationToken)
            .ConfigureAwait(false);

        var added = 0;

        foreach (var (id, name, description, sortOrder) in Types)
        {
            if (existing.Contains(id))
            {
                continue;
            }

            // The system created these, not a person, so the actor columns stay null.
            database.AssetTypes.Add(AssetType.Seed(id, name, description, sortOrder, now));
            added++;
        }

        return added;
    }

    private static async Task<int> SeedStatusesAsync(
        AssetsDbContext database,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await database.AssetStatuses
            .Select(status => status.Id)
            .ToHashSetAsync(cancellationToken)
            .ConfigureAwait(false);

        var added = 0;

        foreach (var (id, code, name, description, sortOrder) in Statuses)
        {
            if (existing.Contains(id))
            {
                continue;
            }

            database.AssetStatuses.Add(AssetStatus.Seed(id, code, name, description, sortOrder, now));
            added++;
        }

        return added;
    }
}
