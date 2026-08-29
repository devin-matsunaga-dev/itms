using Itms.Modules.Identity.Domain;
using Itms.Platform.Identity;
using Itms.Platform.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Itms.Modules.Identity.Seeding;

/// <summary>
/// Creates the three roles, and — in Development only — one account per role so
/// <c>aspire run</c> is the only setup step before someone can sign in.
/// </summary>
/// <remarks>
/// The roles are seeded in every environment, because the system has no meaning without
/// them and they are configuration rather than data. The accounts are not: they carry a
/// password that is published in the README, and an environment check is the only thing
/// standing between a convenience and a back door. It fails closed — anything that is
/// not Development seeds roles and stops.
/// </remarks>
public static class DevelopmentIdentitySeeder
{
    /// <summary>
    /// The password every seeded development account is created with. It is dev-only and
    /// documented, never a secret: production is seeded by the first-run admin setup, and
    /// these accounts must not exist there.
    /// </summary>
    public const string DevelopmentPassword = "Dev!Passw0rd123";

    private static readonly (string Role, string Description)[] Roles =
    [
        (ItmsRoles.Admin, "Complete system management, including administration and the audit log."),
        (ItmsRoles.Technician, "Operational access to tickets, assets, users, monitoring, alerts, knowledge, and reports."),
        (ItmsRoles.User, "Submits and views their own tickets and comments on them."),
    ];

    private static readonly (string UserName, string Email, string DisplayName, string Role)[] DevelopmentAccounts =
    [
        ("admin", "admin@itms.local", "Avery Admin", ItmsRoles.Admin),
        ("tech", "tech@itms.local", "Toni Technician", ItmsRoles.Technician),
        ("user", "user@itms.local", "Uma User", ItmsRoles.User),
    ];

    /// <summary>Seeds roles always, and the development accounts only in Development.</summary>
    /// <param name="services">A scoped service provider.</param>
    /// <param name="cancellationToken">Cancels the seeding.</param>
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var environment = services.GetRequiredService<IHostEnvironment>();
        var roleManager = services.GetRequiredService<RoleManager<ItmsRole>>();
        var userManager = services.GetRequiredService<UserManager<ItmsUser>>();
        var clock = services.GetRequiredService<IClock>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DevelopmentIdentitySeeder));

        foreach (var (role, description) in Roles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await roleManager.RoleExistsAsync(role).ConfigureAwait(false))
            {
                Throw(await roleManager.CreateAsync(ItmsRole.Create(role, description, clock.UtcNow)).ConfigureAwait(false));
            }
        }

        if (!environment.IsDevelopment())
        {
            return;
        }

        foreach (var (userName, email, displayName, role) in DevelopmentAccounts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await userManager.FindByNameAsync(userName).ConfigureAwait(false) is not null)
            {
                continue;
            }

            var user = ItmsUser.Create(userName, email, displayName, clock.UtcNow, actor: null);
            Throw(await userManager.CreateAsync(user, DevelopmentPassword).ConfigureAwait(false));
            Throw(await userManager.AddToRoleAsync(user, role).ConfigureAwait(false));

            IdentityLog.SeededAccount(logger, userName, role);
        }
    }

    // Seeding failing silently would leave a developer staring at a login form that
    // cannot work, so a failure here is exceptional in the sense CONVENTIONS.md means.
    private static void Throw(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Identity seeding failed: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }
}
