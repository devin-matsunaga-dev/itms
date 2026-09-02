using FluentValidation;
using Itms.Contracts.Lookups;
using Itms.Modules.Identity.Authorization;
using Itms.Modules.Identity.Contracts;
using Itms.Modules.Identity.Domain;
using Itms.Modules.Identity.Features.Auth.ChangePassword;
using Itms.Modules.Identity.Features.Auth.CurrentUser;
using Itms.Modules.Identity.Features.Auth.Login;
using Itms.Modules.Identity.Features.Auth.Logout;
using Itms.Modules.Identity.Features.Users.ListUsers;
using Itms.Modules.Identity.Features.Users.SearchUsers;
using Itms.Modules.Identity.Features.Users.UserPanels;
using Itms.Modules.Identity.Persistence;
using Itms.Modules.Identity.Security;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Itms.Modules.Identity;

/// <summary>
/// The Identity module's single registration and single mapping point
/// (ARCHITECTURE.md §3 rule 5).
/// </summary>
public static class IdentityModule
{
    /// <summary>The route prefix every endpoint in this module hangs off.</summary>
    public const string RoutePrefix = "/api/v1/auth";

    /// <summary>
    /// Registers persistence, ASP.NET Core Identity, the cookie handler, the
    /// authorization policies, and this module's handlers.
    /// </summary>
    /// <param name="services">The container. <c>AddPlatform</c> and <c>AddMessaging</c> must already have run.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<ItmsAuthOptions>()
            .BindConfiguration(ItmsAuthOptions.SectionName)
            // Checked at startup, so a bad appsettings value fails the deployment rather
            // than the first sign-in of the day.
            .Validate(o => o.CookieLifetime > TimeSpan.Zero, "Identity:CookieLifetime must be positive.")
            .Validate(o => o.SessionLifetime >= o.CookieLifetime, "Identity:SessionLifetime must be at least CookieLifetime.")
            .Validate(o => o.MaxFailedAccessAttempts is >= 1 and <= 20, "Identity:MaxFailedAccessAttempts must be between 1 and 20.")
            .Validate(o => o.LockoutDuration > TimeSpan.Zero, "Identity:LockoutDuration must be positive.")
            .Validate(o => o.MinimumPasswordLength >= 12, "Identity:MinimumPasswordLength must be at least 12.")
            .Validate(o => o.RateLimitPermits >= 1, "Identity:RateLimitPermits must be at least 1.")
            .Validate(o => o.RateLimitWindow > TimeSpan.Zero, "Identity:RateLimitWindow must be positive.")
            .ValidateOnStart();

        // Built on the connection the ambient session hands out, never on a pool of its
        // own: that is what lets a future handler write a user change and its outbox event
        // in one transaction (STATUS.md, WP-0.4).
        services.AddDbContext<ItmsIdentityDbContext>((provider, builder) =>
            builder.UseNpgsql(
                provider.GetRequiredService<IModuleDbSession>().Connection,
                npgsql => npgsql.MigrationsHistoryTable(
                    ItmsIdentityDbContext.MigrationsHistoryTable,
                    ItmsIdentityDbContext.SchemaName)));

        services
            .AddIdentityCore<ItmsUser>()
            .AddRoles<ItmsRole>()
            .AddEntityFrameworkStores<ItmsIdentityDbContext>()
            .AddClaimsPrincipalFactory<ItmsUserClaimsPrincipalFactory>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        ConfigureIdentityOptions(services);
        ConfigureCookie(services);
        ConfigurePolicies(services);

        services.AddAntiforgery(antiforgery =>
        {
            antiforgery.HeaderName = "X-CSRF-TOKEN";
            antiforgery.Cookie.Name = "itms.csrf";
            antiforgery.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            antiforgery.Cookie.SameSite = SameSiteMode.Lax;
        });

        services.AddIdentityRateLimiting();

        services.TryAddScoped<LoginHandler>();
        services.TryAddScoped<LogoutHandler>();
        services.TryAddScoped<CurrentUserHandler>();
        services.TryAddScoped<ChangePasswordHandler>();
        services.TryAddScoped<ListUsersHandler>();
        services.TryAddScoped<IValidator<LoginRequest>, LoginValidator>();
        services.TryAddScoped<IValidator<ChangePasswordRequest>, ChangePasswordValidator>();

        // The module's public contract. Every other module reads users through this and
        // never sees ItmsUser (ARCHITECTURE.md §3 rule 2).
        services.TryAddScoped<IUserLookup, UserLookupService>();

        // This module's reference count for the directory screens: how many accounts sit
        // in a department or at a location
        // (WP-2.4). TryAddEnumerable rather than TryAddScoped, because every module that
        // holds such a reference registers one and Directory reads all of them.
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IDirectoryUsageLookup, UserDirectoryUsageLookup>());

        return services;
    }

    /// <summary>Maps the authentication endpoints under <see cref="RoutePrefix"/>.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    /// <returns>The route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(RoutePrefix).WithTags("Auth");

        group.MapCsrfToken();
        group.MapLogin();
        group.MapLogout();
        group.MapCurrentUser();
        group.MapChangePassword();

        // Not under the auth group: these are the user directory, guarded by the
        // Technician policy rather than by "any signed-in account".
        endpoints.MapUserDirectory();
        endpoints.MapUserPanels();

        return endpoints;
    }

    /// <summary>
    /// Applies the identity migrations. The host calls this at startup in development;
    /// production applies migrations as a deliberate deployment step.
    /// </summary>
    /// <param name="services">A scoped service provider.</param>
    /// <param name="cancellationToken">Cancels the migration.</param>
    public static async Task MigrateIdentityAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var context = services.GetRequiredService<ItmsIdentityDbContext>();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ConfigureIdentityOptions(IServiceCollection services) =>
        services
            .AddOptions<IdentityOptions>()
            .Configure<IOptions<ItmsAuthOptions>>((identity, auth) =>
            {
                var options = auth.Value;

                // Hardened past the framework defaults, which allow six characters.
                identity.Password.RequiredLength = options.MinimumPasswordLength;
                identity.Password.RequireDigit = true;
                identity.Password.RequireLowercase = true;
                identity.Password.RequireUppercase = true;
                identity.Password.RequireNonAlphanumeric = true;
                identity.Password.RequiredUniqueChars = 4;

                identity.Lockout.AllowedForNewUsers = true;
                identity.Lockout.MaxFailedAccessAttempts = options.MaxFailedAccessAttempts;
                identity.Lockout.DefaultLockoutTimeSpan = options.LockoutDuration;

                identity.User.RequireUniqueEmail = true;

                // No email delivery exists yet, so requiring confirmation would lock
                // everyone out. Revisit when Notifications lands.
                identity.SignIn.RequireConfirmedAccount = false;

                // Platform's ICurrentUser reads the actor's display name from
                // ClaimTypes.Name, so the sign-in name moves aside and the claims factory
                // puts the display name there instead.
                identity.ClaimsIdentity.UserNameClaimType = System.Security.Claims.ClaimTypes.Upn;
            });

    private static void ConfigureCookie(IServiceCollection services)
    {
        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme);

        services
            .AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<ItmsAuthOptions>>((cookie, auth) =>
            {
                var options = auth.Value;

                cookie.Cookie.Name = options.CookieName;
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.Path = "/";

                cookie.ExpireTimeSpan = options.CookieLifetime;
                cookie.SlidingExpiration = true;

                // This is an API. A redirect to an HTML login page would leave a fetch
                // call parsing a form as though it were data (ARCHITECTURE.md §6).
                cookie.Events.OnRedirectToLogin = SessionCookieEvents.OnRedirectToLoginAsync;
                cookie.Events.OnRedirectToAccessDenied = SessionCookieEvents.OnRedirectToAccessDeniedAsync;
                cookie.Events.OnValidatePrincipal = SessionCookieEvents.ValidatePrincipalAsync;
            });
    }

    private static void ConfigurePolicies(IServiceCollection services) =>
        services
            .AddAuthorizationBuilder()
            .AddPolicy(ItmsPolicies.Admin, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(ItmsRoles.Admin))
            // Admin satisfies Technician: SPEC.md §14 gives Admin "complete system
            // management", which is a superset, and an Admin who could not touch a ticket
            // would be a surprise nobody wants to discover during an incident.
            .AddPolicy(ItmsPolicies.Technician, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(ItmsRoles.Technician, ItmsRoles.Admin))
            .AddPolicy(ItmsPolicies.Authenticated, policy => policy
                .RequireAuthenticatedUser());
}
