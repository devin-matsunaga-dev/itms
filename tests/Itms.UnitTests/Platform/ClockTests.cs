using Itms.Platform.Time;

namespace Itms.UnitTests.Platform;

public sealed class ClockTests
{
    [Fact]
    public void The_system_clock_reports_utc()
    {
        new SystemClock().UtcNow.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void The_fake_clock_only_moves_when_the_test_moves_it()
    {
        var clock = new FakeClock();
        var start = clock.UtcNow;

        clock.UtcNow.ShouldBe(start);

        clock.Advance(TimeSpan.FromHours(4));

        clock.UtcNow.ShouldBe(start.AddHours(4));
    }

    [Fact]
    public void The_fake_clock_normalises_a_local_start_to_utc()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.FromHours(2)));

        clock.UtcNow.ShouldBe(new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero));
    }
}
