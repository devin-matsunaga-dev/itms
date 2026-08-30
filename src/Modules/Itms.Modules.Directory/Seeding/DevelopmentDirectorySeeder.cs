using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Itms.Modules.Directory.Seeding;

/// <summary>
/// Fills the directory with a small, realistic organisation in Development, so
/// <c>aspire run</c> is the only setup step before a ticket can be filed against a real
/// department and a real room.
/// </summary>
/// <remarks>
/// Development only, with the environment check inside the seeder rather than only at
/// the call site — the same shape as the identity accounts. Unlike roles, a department
/// is business data: a production deployment starts with an empty directory and an
/// administrator builds the real one, because inventing "Finance" for somebody's actual
/// organisation would be worse than starting blank.
/// </remarks>
public static class DevelopmentDirectorySeeder
{
    private static readonly (string Name, string Code, string Description)[] Departments =
    [
        ("Information Technology", "IT", "Runs the helpdesk, the network, and the estate this system manages."),
        ("Operations", "OPS", "Plant operations and field crews."),
        ("Finance", "FIN", "Accounting, payroll, and procurement."),
        ("Human Resources", "HR", "People, onboarding, and training."),
        ("Engineering", "ENG", "Design, projects, and technical services."),
    ];

    /// <summary>
    /// The development tree, as parent path and node. Ordered so every parent is created
    /// before the children that name it.
    /// </summary>
    private static readonly (string? ParentPath, string Name, LocationKind Kind)[] Locations =
    [
        (null, "Northvale Utilities", LocationKind.Organization),
        ("Northvale Utilities", "Head Office", LocationKind.Site),
        ("Northvale Utilities", "Riverside Treatment Plant", LocationKind.Site),
        ("Northvale Utilities", "Kestrel Pump Station", LocationKind.Site),
        ("Northvale Utilities / Head Office", "Admin Building", LocationKind.Building),
        ("Northvale Utilities / Head Office / Admin Building", "Ground Floor", LocationKind.Floor),
        ("Northvale Utilities / Head Office / Admin Building", "First Floor", LocationKind.Floor),
        ("Northvale Utilities / Head Office / Admin Building / Ground Floor", "Reception", LocationKind.Room),
        ("Northvale Utilities / Head Office / Admin Building / Ground Floor", "Server Room G-04", LocationKind.Room),
        ("Northvale Utilities / Head Office / Admin Building / First Floor", "IT Office 1-12", LocationKind.Room),
        ("Northvale Utilities / Head Office / Admin Building / First Floor", "Finance Office 1-20", LocationKind.Room),
        ("Northvale Utilities / Riverside Treatment Plant", "Control Building", LocationKind.Building),
        ("Northvale Utilities / Riverside Treatment Plant", "Filtration Area", LocationKind.Area),
        ("Northvale Utilities / Riverside Treatment Plant / Control Building", "Control Room", LocationKind.Room),
        // A site with a room and no building: the pump-station shape SPEC.md §5 names,
        // and the reason the hierarchy rule is "ranks descend" rather than "every level
        // is present".
        ("Northvale Utilities / Kestrel Pump Station", "Cabinet A", LocationKind.Room),
    ];

    /// <summary>Seeds the development directory. A no-op outside Development.</summary>
    /// <param name="services">A scoped service provider.</param>
    /// <param name="cancellationToken">Cancels the seeding.</param>
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var environment = services.GetRequiredService<IHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            return;
        }

        var database = services.GetRequiredService<DirectoryDbContext>();
        var clock = services.GetRequiredService<IClock>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DevelopmentDirectorySeeder));
        var now = clock.UtcNow;

        var departments = await SeedDepartmentsAsync(database, now, cancellationToken).ConfigureAwait(false);
        var locations = await SeedLocationsAsync(database, now, cancellationToken).ConfigureAwait(false);

        if (departments > 0 || locations > 0)
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            DirectoryLog.SeededDirectory(logger, departments, locations);
        }
    }

    private static async Task<int> SeedDepartmentsAsync(
        DirectoryDbContext database,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await database.Departments
            .Select(department => department.NormalizedName)
            .ToHashSetAsync(StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        var added = 0;

        foreach (var (name, code, description) in Departments)
        {
            if (existing.Contains(name.ToUpperInvariant()))
            {
                continue;
            }

            // actor: null — the system created these, not a person.
            database.Departments.Add(Department.Create(name, code, description, now, actor: null));
            added++;
        }

        return added;
    }

    private static async Task<int> SeedLocationsAsync(
        DirectoryDbContext database,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var byPath = await database.Locations
            .ToDictionaryAsync(location => location.FullPath, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        var added = 0;

        foreach (var (parentPath, name, kind) in Locations)
        {
            Location? parent = null;

            if (parentPath is not null && !byPath.TryGetValue(parentPath, out parent))
            {
                // The table names its parents by path, and the list is ordered so a parent
                // is always created first. Reaching this means the list itself is wrong.
                throw new InvalidOperationException($"The seed data names a parent that does not exist: '{parentPath}'.");
            }

            var fullPath = Location.ComposeFullPath(parent?.FullPath, name);
            if (byPath.ContainsKey(fullPath))
            {
                continue;
            }

            var location = Location.Create(parent, name, kind, description: null, now, actor: null);
            database.Locations.Add(location);
            byPath[location.FullPath] = location;
            added++;
        }

        return added;
    }
}
