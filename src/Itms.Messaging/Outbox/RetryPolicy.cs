namespace Itms.Messaging.Outbox;

/// <summary>
/// The backoff schedule for a message whose consumer threw. Pure, so it is unit-tested
/// rather than inferred from watching the dispatcher.
/// </summary>
public static class RetryPolicy
{
    /// <summary>The delay before attempt number <paramref name="attempts"/> + 1.</summary>
    /// <param name="attempts">How many attempts have already been made. One or more.</param>
    /// <param name="baseDelay">The delay after the first failure.</param>
    /// <param name="maxDelay">The ceiling the doubling stops at.</param>
    /// <returns>The delay to wait, never longer than <paramref name="maxDelay"/>.</returns>
    public static TimeSpan DelayFor(int attempts, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(baseDelay.Ticks, 0);

        if (baseDelay >= maxDelay)
        {
            return maxDelay;
        }

        // Doubled by shifting, stopping the moment the ceiling is reached. Written as a
        // loop rather than as base * 2^n because MaxAttempts goes to 100 and the closed
        // form overflows a long long before that — silently, and into a negative delay.
        var ticks = baseDelay.Ticks;
        for (var attempt = 1; attempt < attempts && ticks < maxDelay.Ticks; attempt++)
        {
            ticks <<= 1;
        }

        return ticks >= maxDelay.Ticks ? maxDelay : TimeSpan.FromTicks(ticks);
    }
}
