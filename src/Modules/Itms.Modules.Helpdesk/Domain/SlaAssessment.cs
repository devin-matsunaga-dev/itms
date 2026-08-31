namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// Where both of a ticket's SLA clocks stand at one instant.
/// </summary>
/// <remarks>
/// <para>
/// Computed on every read from the stored instants and <c>IClock</c>, never stored. It is
/// the answer to "is this ticket in trouble" that the queue, the detail screen, and
/// Phase 5's reports all ask.
/// </para>
/// <para>
/// <b><see cref="IsPaused"/> is a separate fact from the two states</b>, deliberately. A
/// ticket parked in Waiting may already have breached before it was parked, and a state
/// value of "Paused" would hide that — the technician would see the clock is stopped and
/// not that the target is already gone.
/// </para>
/// </remarks>
/// <param name="Response">Where the response clock stands.</param>
/// <param name="Resolution">Where the resolution clock stands.</param>
/// <param name="IsPaused">Whether the resolution clock is parked because the ticket is Waiting.</param>
public readonly record struct SlaAssessment(SlaState Response, SlaState Resolution, bool IsPaused)
{
    /// <summary>
    /// Reads both clocks of a ticket in <paramref name="status"/> as they stand at
    /// <paramref name="now"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A cancelled ticket stops both clocks with no outcome</b> — see
    /// <see cref="SlaState.Stopped"/>. It is checked first, because a ticket cancelled
    /// while overdue would otherwise keep reporting a breach for a target nobody was ever
    /// going to be held to.
    /// </para>
    /// <para>
    /// <b>A parked resolution clock is judged at the instant it was parked</b>, not at
    /// <paramref name="now"/>: the stored deadline is frozen for the length of the pause,
    /// so comparing it against a moving <c>now</c> would let a ticket drift into a breach
    /// purely by sitting in Waiting — the exact thing SPEC.md §2 says must not happen.
    /// </para>
    /// </remarks>
    /// <param name="sla">The ticket's stored clocks.</param>
    /// <param name="status">Where the ticket sits in the workflow.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <returns>The two states and whether the resolution clock is parked.</returns>
    public static SlaAssessment Of(TicketSla sla, TicketStatus status, DateTimeOffset now)
    {
        if (status == TicketStatus.Cancelled)
        {
            return new SlaAssessment(SlaState.Stopped, SlaState.Stopped, IsPaused: false);
        }

        var response = SlaClock.Evaluate(sla.ResponseDueAt, sla.ResponseWarnAt, sla.RespondedAt, now);

        var resolution = SlaClock.Evaluate(
            sla.ResolutionDueAt,
            sla.ResolutionWarnAt,
            sla.ResolvedAt,
            sla.PausedAt ?? now);

        return new SlaAssessment(response, resolution, sla.IsPaused);
    }
}
