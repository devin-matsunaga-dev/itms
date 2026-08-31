using Itms.Messaging.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace Itms.IntegrationTests.AuditModule;

/// <summary>
/// Drives the outbox dispatcher by hand, for the assertions that cannot wait for it.
/// </summary>
/// <remarks>
/// <see cref="Eventually"/> covers the assertions about what the dispatcher <em>did</em>
/// deliver. This covers the opposite ones: an absence proved by waiting for a timeout is
/// an absence that passes for the wrong reason, and would keep passing on the day the
/// event started being published. Forcing one pass turns "nothing was published" into
/// something a test can actually distinguish from "nothing has arrived yet".
/// </remarks>
internal static class OutboxClient
{
    /// <summary>Runs the dispatcher once, delivering everything currently staged.</summary>
    /// <param name="services">The host's provider.</param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    public static async Task ProcessOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().ProcessOnceAsync(cancellationToken);
    }
}
