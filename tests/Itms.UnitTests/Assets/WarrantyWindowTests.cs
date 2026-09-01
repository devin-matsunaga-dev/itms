using Itms.Modules.Assets.Features.Assets.ListAssets;

namespace Itms.UnitTests.Assets;

/// <summary>
/// The bounds behind <c>warrantyExpiringInDays</c>. The filter around them is a
/// <c>WHERE</c> clause a reader can check by eye; this is the part that can be quietly
/// wrong, and neither an off-by-one nor an overflow is something a test can read back off a
/// rendered list.
/// </summary>
public sealed class WarrantyWindowTests
{
    /// <summary>An ordinary date, far from either end of the calendar.</summary>
    private static readonly DateOnly Today = new(2026, 9, 1);

    /// <summary>
    /// The window is inclusive at the top: asking for thirty days includes a warranty
    /// running out on the thirtieth day, not the twenty-ninth.
    /// </summary>
    [Theory]
    [InlineData(0, "2026-09-01")]
    [InlineData(1, "2026-09-02")]
    [InlineData(30, "2026-10-01")]
    [InlineData(365, "2027-09-01")]
    public void The_window_ends_exactly_n_days_from_today(int days, string expected)
    {
        WarrantyWindow.UpperBound(Today, days).ShouldBe(DateOnly.Parse(expected, null));
    }

    /// <summary>
    /// Zero days is the warranties running out today — the lower bound is <c>today</c> and
    /// the upper bound is too, so the window is exactly one day wide rather than empty.
    /// </summary>
    [Fact]
    public void Zero_days_is_the_warranties_running_out_today()
    {
        WarrantyWindow.UpperBound(Today, 0).ShouldBe(Today);
    }

    /// <summary>
    /// A negative window matches nothing rather than failing. The upper bound falls below
    /// the lower one, so the range is empty — which is what "expiring within minus five
    /// days" describes, and the reading WP-1.5 chose for a created-date range whose end
    /// precedes its start.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(-30)]
    [InlineData(int.MinValue)]
    public void A_negative_window_ends_before_it_starts(int days)
    {
        WarrantyWindow.UpperBound(Today, days).ShouldBeLessThan(Today);
    }

    /// <summary>
    /// The clamp is not decorative: <see cref="DateOnly.AddDays"/> throws once the result
    /// leaves the calendar, so an unbounded integer off the query string would turn a filter
    /// into a 500.
    /// </summary>
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(4_000_000)]
    public void An_absurd_window_saturates_at_the_end_of_the_calendar(int days)
    {
        WarrantyWindow.UpperBound(Today, days).ShouldBe(DateOnly.MaxValue);
    }

    /// <summary>The other end of the same guard, which a negative value walks towards.</summary>
    [Fact]
    public void An_absurd_negative_window_saturates_at_the_start_of_the_calendar()
    {
        WarrantyWindow.UpperBound(Today, int.MinValue).ShouldBe(DateOnly.MinValue);
    }

    /// <summary>
    /// The saturation holds at the very edges too, where <c>today</c> itself is the boundary
    /// and the remaining headroom is zero.
    /// </summary>
    [Fact]
    public void The_clamp_holds_when_today_is_already_the_boundary()
    {
        WarrantyWindow.UpperBound(DateOnly.MaxValue, 10).ShouldBe(DateOnly.MaxValue);
        WarrantyWindow.UpperBound(DateOnly.MinValue, -10).ShouldBe(DateOnly.MinValue);
    }

    /// <summary>
    /// A leap day is not a special case — the arithmetic is in days, not in months — but it
    /// is the shape an off-by-one hides in, so it is asserted rather than assumed.
    /// </summary>
    [Fact]
    public void The_window_crosses_a_leap_day_without_help()
    {
        WarrantyWindow.UpperBound(new DateOnly(2028, 2, 27), 3).ShouldBe(new DateOnly(2028, 3, 1));
    }

    /// <summary>
    /// "Today" is the UTC date, because ARCHITECTURE.md §11 stores every instant that way
    /// and a warranty date is compared against dates an operator typed.
    /// </summary>
    [Fact]
    public void Today_is_read_in_utc_and_not_in_the_offset_of_the_instant()
    {
        // 2026-09-01T23:30 at +05:00 is still 2026-09-01 in UTC — 18:30.
        var instant = new DateTimeOffset(2026, 9, 1, 23, 30, 0, TimeSpan.FromHours(5));

        WarrantyWindow.Today(instant).ShouldBe(new DateOnly(2026, 9, 1));
    }

    /// <summary>The same instant on the other side of the boundary, so the assertion above cuts.</summary>
    [Fact]
    public void Today_rolls_over_on_the_utc_boundary()
    {
        var instant = new DateTimeOffset(2026, 9, 1, 23, 30, 0, TimeSpan.FromHours(-5));

        WarrantyWindow.Today(instant).ShouldBe(new DateOnly(2026, 9, 2));
    }
}
