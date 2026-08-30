namespace Itms.IntegrationTests.AuditModule;

/// <summary>
/// Waits for a condition instead of sleeping for a guess.
/// </summary>
/// <remarks>
/// CONVENTIONS.md bans <c>Thread.Sleep</c> in tests and asks for a timeout helper
/// instead. The audit-event tests need one because the host under test runs the real
/// outbox dispatcher on a timer: the assertion is about what the dispatcher eventually
/// wrote, and a fixed delay would be either flaky or slow.
/// </remarks>
internal static class Eventually
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

    /// <summary>Polls <paramref name="condition"/> until it holds, or fails the test.</summary>
    /// <param name="condition">The condition to wait for.</param>
    /// <param name="because">What was being waited for, for the failure message.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <param name="timeout">How long to wait. Defaults to fifteen seconds.</param>
    public static async Task UntilAsync(
        Func<Task<bool>> condition,
        string because,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(condition);

        var deadline = DateTimeOffset.UtcNow + (timeout ?? DefaultTimeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(Interval, cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for {because}.");
    }
}
