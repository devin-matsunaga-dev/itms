namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// The SLA arithmetic, written once: where the 80% mark falls, and what a clock's
/// position against its target means.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure, and deliberately knows nothing about a ticket.</b> Every input is an instant
/// or a span, so the unit suite can walk the boundaries exactly rather than approaching
/// them with a real clock — which is the whole reason <c>IClock</c> exists
/// (CONVENTIONS.md bans <c>Thread.Sleep</c> in tests).
/// </para>
/// <para>
/// <b>The two boundaries are not the same comparison, and that is the point.</b> A clock
/// still running has breached the moment it <em>reaches</em> its due instant: the deadline
/// has arrived and nothing has happened. A clock that stopped exactly at its due instant
/// has <em>met</em> it: a four-hour target promises resolution within four hours, and four
/// hours exactly is within. So a running clock breaches at <c>&gt;=</c> and a stopped one
/// at <c>&gt;</c>. WP-1.8's criterion asks for the exact boundary at 100% to be tested;
/// this is what it is being tested against.
/// </para>
/// </remarks>
public static class SlaClock
{
    /// <summary>
    /// How much of a target must be consumed before the clock reads
    /// <see cref="SlaState.Approaching"/>. SPEC.md §2 fixes it at 80%.
    /// </summary>
    public const int WarnPercent = 80;

    /// <summary>
    /// Where the <see cref="SlaState.Approaching"/> mark falls for a target starting at
    /// <paramref name="start"/>.
    /// </summary>
    /// <remarks>
    /// Computed in ticks as <c>× 80 ÷ 100</c> rather than <c>× 0.8</c>, so the mark is the
    /// same instant on every machine and every run: binary floating point cannot hold 0.8,
    /// and an 80% boundary that lands a tick either side of where a test puts it is a
    /// flake nobody enjoys. Integer division truncates, which can move the mark at most
    /// one tick earlier — a hundred-nanosecond error on a target measured in minutes.
    /// </remarks>
    /// <param name="start">When the clock started.</param>
    /// <param name="target">How long the target allows.</param>
    /// <returns>The instant at which <see cref="WarnPercent"/> of the target is consumed.</returns>
    public static DateTimeOffset WarnPoint(DateTimeOffset start, TimeSpan target) =>
        start + TimeSpan.FromTicks(target.Ticks * WarnPercent / 100);

    /// <summary>
    /// Reads one clock's position.
    /// </summary>
    /// <param name="dueAt">When the target expires.</param>
    /// <param name="warnAt">When 80% of it is consumed — <see cref="WarnPoint"/>.</param>
    /// <param name="stoppedAt">
    /// When the clock stopped for good — the response was made, or the ticket was resolved
    /// — or <see langword="null"/> while it is still running.
    /// </param>
    /// <param name="runningAt">
    /// The instant a still-running clock is judged at. Normally <c>IClock.UtcNow</c>; for
    /// a resolution clock parked in Waiting it is the instant it was parked, because no
    /// time has been consumed since.
    /// </param>
    /// <returns>The state that pair of instants describes.</returns>
    public static SlaState Evaluate(
        DateTimeOffset dueAt,
        DateTimeOffset warnAt,
        DateTimeOffset? stoppedAt,
        DateTimeOffset runningAt)
    {
        if (stoppedAt is { } stopped)
        {
            // Strictly after: stopping on the due instant is meeting the target.
            return stopped > dueAt ? SlaState.Breached : SlaState.Met;
        }

        if (runningAt >= dueAt)
        {
            // At or after: the deadline has arrived and the work has not.
            return SlaState.Breached;
        }

        return runningAt >= warnAt ? SlaState.Approaching : SlaState.Pending;
    }
}
