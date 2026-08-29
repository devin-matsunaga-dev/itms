using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Itms.Modules.Identity.Security;

/// <summary>
/// The rate limit on the credential endpoints. CONVENTIONS.md's security floor requires
/// one on login; without it, lockout only slows an attacker down per account, and a
/// password spray across many accounts never trips it.
/// </summary>
public static class IdentityRateLimiting
{
    /// <summary>The policy name the credential endpoints attach to.</summary>
    public const string PolicyName = "identity.credentials";

    /// <summary>Registers the policy. The host is what calls <c>UseRateLimiter</c>.</summary>
    /// <param name="services">The container.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddIdentityRateLimiting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.AddPolicy(PolicyName, httpContext =>
            {
                var options = httpContext.RequestServices.GetRequiredService<IOptions<ItmsAuthOptions>>().Value;

                // Partitioned by address, not by account: the attack this stops is one
                // source trying many accounts, which an account-keyed limit cannot see.
                return RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.RateLimitPermits,
                        Window = options.RateLimitWindow,
                        QueueLimit = 0,
                    });
            });
        });

        return services;
    }
}
