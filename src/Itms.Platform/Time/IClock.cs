namespace Itms.Platform.Time;

/// <summary>
/// The system's only source of "now". It exists so SLA math, availability windows,
/// and expiry checks can be tested without <c>Thread.Sleep</c> — CONVENTIONS.md
/// bans sleeping in tests precisely because this abstraction is here.
/// </summary>
public interface IClock
{
    /// <summary>The current instant in UTC. ARCHITECTURE.md §11 invariant 11: all times are stored UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
