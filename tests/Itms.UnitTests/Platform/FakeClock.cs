using Itms.Platform.Time;

namespace Itms.UnitTests.Platform;

/// <summary>
/// A clock the test moves by hand. CONVENTIONS.md bans <c>Thread.Sleep</c> in tests;
/// every "wait for the SLA to breach" scenario is written as an <see cref="Advance"/>
/// instead.
/// </summary>
/// <param name="now">The instant the clock starts at.</param>
public sealed class FakeClock(DateTimeOffset now) : IClock
{
    /// <summary>A fixed, arbitrary instant to anchor tests that only need "some time".</summary>
    public static DateTimeOffset DefaultNow { get; } = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A clock starting at <see cref="DefaultNow"/>.</summary>
    public FakeClock()
        : this(DefaultNow)
    {
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; private set; } = now.ToUniversalTime();

    /// <summary>Moves the clock forward.</summary>
    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
