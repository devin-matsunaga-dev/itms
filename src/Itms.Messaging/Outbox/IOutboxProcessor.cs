namespace Itms.Messaging.Outbox;

/// <summary>
/// One pass of the dispatcher, separated from the hosted service that loops it.
/// </summary>
/// <remarks>
/// The split is what lets the integration tests drive delivery deterministically:
/// they call one pass and assert, instead of starting a background loop and waiting
/// on it — which CONVENTIONS.md's ban on <c>Thread.Sleep</c> in tests rules out anyway.
/// </remarks>
public interface IOutboxProcessor
{
    /// <summary>Claims a batch of outstanding messages and delivers each to its consumers.</summary>
    /// <param name="cancellationToken">Stops the pass. Messages already claimed keep their lease and are retried.</param>
    /// <returns>How many messages were claimed. Zero means there was nothing to do.</returns>
    Task<int> ProcessOnceAsync(CancellationToken cancellationToken = default);
}
