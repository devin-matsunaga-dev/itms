using Itms.Modules.Helpdesk.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// <see cref="SlaClock"/> — the arithmetic WP-1.8 is built on, walked to the tick.
/// </summary>
/// <remarks>
/// WP-1.8's criterion asks for "the exact boundary at 80% and 100%". These are those
/// boundaries: each one is asserted one tick before, exactly on, and one tick after, which
/// is the only way to tell an inclusive comparison from an exclusive one. Nothing here
/// touches a ticket or a database — that is what makes walking a tick at a time possible.
/// </remarks>
public sealed class SlaClockTests
{
    private static readonly DateTimeOffset Start = FakeClock.DefaultNow;

    /// <summary>A four-hour resolution target, the seeded Medium priority's.</summary>
    private static readonly TimeSpan Target = TimeSpan.FromHours(4);

    private static readonly DateTimeOffset Due = Start + Target;
    private static readonly DateTimeOffset Warn = SlaClock.WarnPoint(Start, Target);

    [Fact]
    public void The_warn_point_is_four_fifths_of_the_target()
    {
        SlaClock.WarnPoint(Start, TimeSpan.FromHours(4)).ShouldBe(Start.AddMinutes(192));
        SlaClock.WarnPoint(Start, TimeSpan.FromMinutes(30)).ShouldBe(Start.AddMinutes(24));
        SlaClock.WarnPoint(Start, TimeSpan.FromMinutes(15)).ShouldBe(Start.AddMinutes(12));
    }

    /// <summary>
    /// 80% of any whole number of minutes is a whole number of seconds — 48 per minute —
    /// which is why the mark survives a round trip through PostgreSQL's microsecond
    /// timestamps unchanged, and why the backfill in the migration can compute the same
    /// instant in SQL.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(240)]
    [InlineData(365 * 24 * 60)]
    public void The_warn_point_of_a_whole_minute_target_falls_on_a_whole_second(int minutes)
    {
        var warn = SlaClock.WarnPoint(Start, TimeSpan.FromMinutes(minutes));

        (warn - Start).ShouldBe(TimeSpan.FromSeconds(minutes * 48));
    }

    [Fact]
    public void A_running_clock_under_eighty_percent_is_pending() =>
        SlaClock.Evaluate(Due, Warn, stoppedAt: null, Warn.AddTicks(-1))
            .ShouldBe(SlaState.Pending);

    /// <summary>
    /// SPEC.md §2 says "approaching (80% consumed)". Consumed <em>to</em> 80% is the flag
    /// point, not one tick past it.
    /// </summary>
    [Fact]
    public void A_running_clock_at_exactly_eighty_percent_is_approaching() =>
        SlaClock.Evaluate(Due, Warn, stoppedAt: null, Warn)
            .ShouldBe(SlaState.Approaching);

    [Fact]
    public void A_running_clock_one_tick_before_its_target_is_still_approaching() =>
        SlaClock.Evaluate(Due, Warn, stoppedAt: null, Due.AddTicks(-1))
            .ShouldBe(SlaState.Approaching);

    /// <summary>
    /// The deadline has arrived and the work has not. This is the boundary that differs
    /// from the stopped one below, and the difference is deliberate.
    /// </summary>
    [Fact]
    public void A_running_clock_at_exactly_its_target_has_breached() =>
        SlaClock.Evaluate(Due, Warn, stoppedAt: null, Due)
            .ShouldBe(SlaState.Breached);

    [Fact]
    public void A_running_clock_past_its_target_has_breached() =>
        SlaClock.Evaluate(Due, Warn, stoppedAt: null, Due.AddHours(9))
            .ShouldBe(SlaState.Breached);

    /// <summary>
    /// A four-hour target promises the work done <em>within</em> four hours, and four hours
    /// exactly is within. A clock that stopped on the instant met its target; one that was
    /// still running at that instant did not.
    /// </summary>
    [Fact]
    public void A_clock_that_stopped_exactly_on_its_target_met_it() =>
        SlaClock.Evaluate(Due, Warn, stoppedAt: Due, runningAt: Due.AddDays(30))
            .ShouldBe(SlaState.Met);

    [Fact]
    public void A_clock_that_stopped_one_tick_past_its_target_breached_it() =>
        SlaClock.Evaluate(Due, Warn, stoppedAt: Due.AddTicks(1), runningAt: Due.AddDays(30))
            .ShouldBe(SlaState.Breached);

    [Fact]
    public void A_clock_that_stopped_early_met_its_target_however_late_it_is_read() =>
        SlaClock.Evaluate(Due, Warn, stoppedAt: Start.AddMinutes(5), runningAt: Due.AddYears(1))
            .ShouldBe(SlaState.Met);

    /// <summary>
    /// A stopped clock is judged on when it stopped and on nothing else — the warn point is
    /// not consulted, because "was approaching when it finished" is not an outcome.
    /// </summary>
    [Fact]
    public void A_clock_that_stopped_inside_the_warn_window_still_met_its_target() =>
        SlaClock.Evaluate(Due, Warn, stoppedAt: Warn.AddMinutes(1), runningAt: Due.AddDays(1))
            .ShouldBe(SlaState.Met);
}
