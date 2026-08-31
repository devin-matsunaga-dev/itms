using Itms.Platform.Http;
using Itms.Platform.Identity;
using Itms.Platform.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Itms.Platform;

/// <summary>
/// Registers the shared kernel. Every module depends on <see cref="IClock"/> and
/// <see cref="ICurrentUser"/>, so the host calls this once before any
/// <c>AddXxxModule</c> and no module registers its own copy.
/// </summary>
public static class PlatformServiceCollectionExtensions
{
    /// <summary>Adds the clock, the current-user accessor, and RFC 7807 problem details.</summary>
    /// <param name="services">The container.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddPlatform(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.TryAddSingleton<IClock, SystemClock>();

        // Scoped: it reads the principal of the request in flight.
        services.TryAddScoped<ICurrentUser, HttpContextCurrentUser>();

        // Makes the framework's own 400/401/403/404/500 responses ProblemDetails too, so
        // "errors are ProblemDetails, always" holds for the ones no handler produced.
        services.AddProblemDetails();

        // ...and for the ones thrown during model binding, before any endpoint filter can
        // run. Without this a body the framework cannot parse is a 500 (WP-1.3).
        services.AddExceptionHandler<MalformedRequestExceptionHandler>();

        return services;
    }
}
