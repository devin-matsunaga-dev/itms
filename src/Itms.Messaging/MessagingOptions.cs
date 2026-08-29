namespace Itms.Messaging;

/// <summary>
/// How the dispatcher paces itself. Every value has a working default; the host only
/// overrides one when a measurement says to.
/// </summary>
public sealed class MessagingOptions
{
    /// <summary>The configuration section these bind from.</summary>
    public const string SectionName = "Messaging";

    /// <summary>How many messages one dispatcher pass claims. Larger batches trade latency for fewer round trips.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>How long the dispatcher waits after an empty pass before looking again.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long a claimed message stays invisible to other dispatchers. It must comfortably
    /// exceed the slowest consumer: if the lease lapses mid-flight, a second dispatcher will
    /// pick the message up while the first is still working it.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>The delay before the first retry. Each subsequent attempt doubles it.</summary>
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>The ceiling on the doubling, so a long-broken consumer is retried steadily rather than never.</summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How many attempts a message gets before it is parked. It is never deleted — a
    /// dead-lettered event is the evidence that something needs looking at.
    /// </summary>
    public int MaxAttempts { get; set; } = 10;
}
