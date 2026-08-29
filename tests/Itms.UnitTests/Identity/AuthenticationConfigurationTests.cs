using System.Security.Claims;
using Itms.Modules.Identity;
using Itms.Modules.Identity.Security;
using Itms.Platform;
using Itms.Platform.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Itms.UnitTests.Identity;

/// <summary>
/// The authentication settings ARCHITECTURE.md §7 and CONVENTIONS.md pin down. They are
/// configuration rather than logic, which is exactly why they need a test: nothing else
/// would notice a default quietly reasserting itself.
/// </summary>
public sealed class AuthenticationConfigurationTests
{
    [Fact]
    public void The_password_policy_is_hardened_past_the_framework_defaults()
    {
        using var provider = BuildProvider();

        var identity = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        identity.Password.RequiredLength.ShouldBeGreaterThanOrEqualTo(12);
        identity.Password.RequireDigit.ShouldBeTrue();
        identity.Password.RequireLowercase.ShouldBeTrue();
        identity.Password.RequireUppercase.ShouldBeTrue();
        identity.Password.RequireNonAlphanumeric.ShouldBeTrue();
        identity.User.RequireUniqueEmail.ShouldBeTrue();
    }

    [Fact]
    public void Lockout_is_on_for_new_users()
    {
        using var provider = BuildProvider();

        var identity = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        // A lockout that only applies to accounts someone remembered to opt in is not one.
        identity.Lockout.AllowedForNewUsers.ShouldBeTrue();
        identity.Lockout.MaxFailedAccessAttempts.ShouldBe(5);
        identity.Lockout.DefaultLockoutTimeSpan.ShouldBe(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void The_session_cookie_is_http_only_secure_same_site_lax_and_sliding()
    {
        using var provider = BuildProvider();

        var cookie = provider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        cookie.Cookie.HttpOnly.ShouldBeTrue();
        cookie.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.Always);
        cookie.Cookie.SameSite.ShouldBe(SameSiteMode.Lax);
        cookie.SlidingExpiration.ShouldBeTrue();
        cookie.Cookie.Name.ShouldBe("itms.session");
    }

    [Fact]
    public void The_display_name_claim_is_where_the_shared_kernel_looks_for_it()
    {
        using var provider = BuildProvider();

        var identity = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        // Platform's ICurrentUser reads ClaimTypes.Name; if Identity put the sign-in name
        // there, every audit row and ticket would show "tech" instead of a person's name.
        identity.ClaimsIdentity.UserNameClaimType.ShouldBe(ClaimTypes.Upn);
        identity.ClaimsIdentity.RoleClaimType.ShouldBe(ClaimTypes.Role);
    }

    [Fact]
    public void A_password_minimum_below_the_floor_fails_at_startup()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Identity:MinimumPasswordLength"] = "8",
        });

        // Validated when the options are built, so a bad deployment setting fails loudly
        // rather than silently weakening the policy.
        Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ItmsAuthOptions>>().Value);
    }

    [Fact]
    public void A_session_shorter_than_the_cookie_fails_at_startup() =>
        Should.Throw<OptionsValidationException>(() =>
        {
            using var provider = BuildProvider(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Identity:CookieLifetime"] = "12:00:00",
                ["Identity:SessionLifetime"] = "01:00:00",
            });

            return provider.GetRequiredService<IOptions<ItmsAuthOptions>>().Value;
        });

    [Theory]
    [InlineData(ItmsRoles.Admin, ItmsPolicies.Admin, true)]
    [InlineData(ItmsRoles.Admin, ItmsPolicies.Technician, true)]
    [InlineData(ItmsRoles.Admin, ItmsPolicies.Authenticated, true)]
    [InlineData(ItmsRoles.Technician, ItmsPolicies.Admin, false)]
    [InlineData(ItmsRoles.Technician, ItmsPolicies.Technician, true)]
    [InlineData(ItmsRoles.Technician, ItmsPolicies.Authenticated, true)]
    [InlineData(ItmsRoles.User, ItmsPolicies.Admin, false)]
    [InlineData(ItmsRoles.User, ItmsPolicies.Technician, false)]
    [InlineData(ItmsRoles.User, ItmsPolicies.Authenticated, true)]
    public async Task The_role_matrix_is_what_spec_section_14_says_it_is(string role, string policy, bool allowed)
    {
        using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var principal = PrincipalWith(role);

        var result = await authorization.AuthorizeAsync(principal, resource: null, policy);

        result.Succeeded.ShouldBe(allowed);
    }

    [Fact]
    public async Task An_anonymous_principal_satisfies_no_policy()
    {
        using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        foreach (var policy in new[] { ItmsPolicies.Admin, ItmsPolicies.Technician, ItmsPolicies.Authenticated })
        {
            (await authorization.AuthorizeAsync(anonymous, resource: null, policy)).Succeeded.ShouldBeFalse();
        }
    }

    private static ClaimsPrincipal PrincipalWith(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], authenticationType: "test"));

    private static ServiceProvider BuildProvider(Dictionary<string, string?>? settings = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build());
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddPlatform();
        services.AddIdentityModule();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
