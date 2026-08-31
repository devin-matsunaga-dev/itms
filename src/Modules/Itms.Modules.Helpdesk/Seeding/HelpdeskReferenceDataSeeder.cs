using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Itms.Modules.Helpdesk.Seeding;

/// <summary>
/// Puts the ticket categories and priorities SPEC.md §2 names into an empty database.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every environment, unlike the development directory.</b> A department is business
/// data, and inventing "Finance" for somebody's actual organisation would be worse than
/// starting blank. A category and a priority are reference data: a deployment with no
/// priorities could not accept a ticket at all. This is the same reason Identity seeds
/// its roles everywhere.
/// </para>
/// <para>
/// <b>Why a seeder rather than the migration WP-1.1's text names.</b> Reference data
/// inserted by a migration cannot be restored once removed, and the integration suite
/// removes it on every test: CONVENTIONS.md fixes the between-test reset as a truncate
/// via Respawn, and a truncate empties a migration-seeded table with nothing able to put
/// the rows back. Re-running migrations per test is the alternative CONVENTIONS.md
/// explicitly rules out. A seeder is idempotent, callable, and is already the shape
/// Identity's roles use.
/// </para>
/// <para>
/// Idempotent <em>by id</em>, not by name: the ids are literals, so re-running this finds
/// the same rows even after an administrator has renamed one, and a rename therefore
/// survives a restart. It never reactivates a retired row and never edits an existing
/// one — an operator's decisions outrank the seed.
/// </para>
/// </remarks>
public static class HelpdeskReferenceDataSeeder
{
    /// <summary>The eight categories SPEC.md §2 names, in the order it names them.</summary>
    private static readonly (Guid Id, string Name, string Description, int SortOrder)[] Categories =
    [
        (new("01a052f8-4f00-785d-b254-c82d3c95840f"), "Hardware", "Desktops, laptops, peripherals, and physical faults.", 10),
        (new("01a052f8-4f01-7c72-a4d7-febcc1a2289f"), "Software", "Applications, licensing, installation, and errors.", 20),
        (new("01a052f8-4f02-7957-bf25-7f52ec9f109c"), "Network", "Connectivity, wireless, VPN, and network performance.", 30),
        (new("01a052f8-4f03-7dfb-a8c6-6c1e4b876ec5"), "Account/Access", "Passwords, permissions, and access requests.", 40),
        (new("01a052f8-4f04-7c47-aeac-de872a7233b8"), "Microsoft 365", "Mail, Teams, SharePoint, OneDrive, and Office applications.", 50),
        (new("01a052f8-4f05-7d57-8cf2-cd46b59db879"), "Printer", "Printing, scanning, and multifunction devices.", 60),
        (new("01a052f8-4f06-76e7-bfa6-4f3c6e2b6415"), "Security", "Suspected compromise, phishing, malware, and policy concerns.", 70),
        (new("01a052f8-4f07-7287-a4f8-3dcfa55c33be"), "Other", "Anything that does not fit the categories above.", 80),
    ];

    // sort_order in tens, so a ninth category can be slotted between two existing ones
    // without renumbering the list.

    /// <summary>
    /// The four priorities, most urgent first, with their starting SLA targets in
    /// minutes.
    /// </summary>
    /// <remarks>
    /// The targets are starting values, not policy: every one is editable through
    /// <c>PUT /api/v1/ticket-priorities/{id}</c>, and nothing computes against them until
    /// WP-1.8.
    /// </remarks>
    private static readonly (Guid Id, string Code, string Name, string Description, int Rank, int Response, int Resolution)[] Priorities =
    [
        (new("01a052f8-4f08-7f95-90d3-b78147148662"), "critical", "Critical", "Service is down or unsafe; work stops until it is fixed.", 1, 15, 240),
        (new("01a052f8-4f09-77b3-8215-aa2a19f2ccb9"), "high", "High", "A person or a team is blocked, with no workaround.", 2, 60, 480),
        (new("01a052f8-4f0a-7d83-bd52-4d513f13fd4e"), "medium", "Medium", "Work is degraded but a workaround exists.", 3, 240, 1440),
        (new("01a052f8-4f0b-753b-9319-9ec9a0705361"), "low", "Low", "A request or a minor issue with no operational impact.", 4, 480, 4320),
    ];

    /// <summary>The ids of the seeded categories, so a caller can name one without a lookup.</summary>
    public static IReadOnlyList<Guid> CategoryIds { get; } = [.. Categories.Select(category => category.Id)];

    /// <summary>The ids of the seeded priorities, most urgent first.</summary>
    public static IReadOnlyList<Guid> PriorityIds { get; } = [.. Priorities.Select(priority => priority.Id)];

    /// <summary>Seeds anything missing. Safe to run on every startup.</summary>
    /// <param name="services">A scoped service provider.</param>
    /// <param name="cancellationToken">Cancels the seeding.</param>
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var database = services.GetRequiredService<HelpdeskDbContext>();
        var clock = services.GetRequiredService<IClock>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(HelpdeskReferenceDataSeeder));
        var now = clock.UtcNow;

        var categories = await SeedCategoriesAsync(database, now, cancellationToken).ConfigureAwait(false);
        var priorities = await SeedPrioritiesAsync(database, now, cancellationToken).ConfigureAwait(false);

        if (categories > 0 || priorities > 0)
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            HelpdeskLog.SeededReferenceData(logger, categories, priorities);
        }
    }

    private static async Task<int> SeedCategoriesAsync(
        HelpdeskDbContext database,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await database.TicketCategories
            .Select(category => category.Id)
            .ToHashSetAsync(cancellationToken)
            .ConfigureAwait(false);

        var added = 0;

        foreach (var (id, name, description, sortOrder) in Categories)
        {
            if (existing.Contains(id))
            {
                continue;
            }

            // The system created these, not a person, so the actor columns stay null.
            database.TicketCategories.Add(TicketCategory.Seed(id, name, description, sortOrder, now));
            added++;
        }

        return added;
    }

    private static async Task<int> SeedPrioritiesAsync(
        HelpdeskDbContext database,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await database.TicketPriorities
            .Select(priority => priority.Id)
            .ToHashSetAsync(cancellationToken)
            .ConfigureAwait(false);

        var added = 0;

        foreach (var (id, code, name, description, rank, response, resolution) in Priorities)
        {
            if (existing.Contains(id))
            {
                continue;
            }

            database.TicketPriorities.Add(
                TicketPriority.Seed(id, code, name, description, rank, response, resolution, now));
            added++;
        }

        return added;
    }
}
