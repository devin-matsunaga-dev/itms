namespace Itms.Modules.Assets.Features.Assets.ListAssets;

/// <summary>
/// The date arithmetic behind the warranty filters, kept out of the query that uses it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure, because it is the part that can be wrong.</b> The rest of the warranty filter
/// is a <c>WHERE</c> clause a reader can check by eye; the bounds are an off-by-one and an
/// overflow waiting to happen, and neither is something a test can read back off a rendered
/// list. WP-1.11 made the same split for the SLA meter's fraction and for the same reason.
/// </para>
/// <para>
/// <b>"Today" comes from <c>IClock</c>, not from the caller.</b> Unlike WP-1.12's
/// <c>dueBefore</c>, which is an instant and therefore a question about where the caller is
/// standing, a warranty expiry is a <see cref="DateOnly"/> — a calendar fact with no time
/// and no zone. Asking a client to manufacture a date window would buy nothing and would
/// give two clients two answers to one question.
/// </para>
/// <para>
/// <b>Public, following WP-1.3's call for <c>TicketStateMachine</c> and WP-1.5's for
/// <c>TicketETag</c>.</b> These bounds decide which assets a warranty filter returns, this
/// repository has no <c>InternalsVisibleTo</c>, and an arithmetic guard that has never been
/// executed is a guard nobody has checked.
/// </para>
/// </remarks>
public static class WarrantyWindow
{
    /// <summary>
    /// The last day a warranty may expire on and still count as "expiring within
    /// <paramref name="days"/> days".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inclusive at both ends: <c>warrantyExpiringInDays=0</c> is the warranties running out
    /// today, and <c>=30</c> includes one running out on the thirtieth day. Already-lapsed
    /// warranties are excluded by the lower bound, which is <paramref name="today"/> itself.
    /// </para>
    /// <para>
    /// <b>A negative window matches nothing, and that is the answer rather than an error.</b>
    /// The upper bound falls below the lower one and the range is empty — which is precisely
    /// what "expiring within minus five days" describes. It is the reading WP-1.5 chose for a
    /// created-date range whose end precedes its start.
    /// </para>
    /// <para>
    /// <b>The clamp is not decorative.</b> <see cref="DateOnly.AddDays"/> throws once the
    /// result leaves the calendar, so an unbounded integer off the query string would turn
    /// a filter into a 500. Saturating at <see cref="DateOnly.MaxValue"/> answers the
    /// question the caller actually asked — every asset that has a warranty date at all.
    /// </para>
    /// </remarks>
    /// <param name="today">The current date, from <c>IClock</c>.</param>
    /// <param name="days">How many days ahead to look. May be negative or absurd.</param>
    /// <returns>The inclusive upper bound of the window.</returns>
    public static DateOnly UpperBound(DateOnly today, int days)
    {
        if (days < 0)
        {
            // Below `today`, so the range is empty however far below it lands. Computed
            // rather than returned as a constant so the caller's lower bound still decides.
            var floor = DateOnly.MinValue.DayNumber - today.DayNumber;
            return today.AddDays(Math.Max(days, floor));
        }

        var ceiling = DateOnly.MaxValue.DayNumber - today.DayNumber;
        return today.AddDays(Math.Min(days, ceiling));
    }

    /// <summary>The current date in UTC, from an instant.</summary>
    /// <remarks>
    /// UTC because ARCHITECTURE.md §11 stores every instant that way and a warranty date is
    /// compared against dates the operator typed, not against a wall clock.
    /// </remarks>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <returns>Today's date.</returns>
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);
}
