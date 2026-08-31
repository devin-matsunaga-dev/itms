using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.Tickets.ListTickets;

/// <summary>
/// Narrows the queue to the tickets whose <em>resolution</em> clock is in a given state —
/// the server side of WP-1.9's "Overdue" view.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the SLA rule written a second time, in SQL, and that is a real cost.</b>
/// <see cref="SlaAssessment"/> is the one a response is built from; this is the one a
/// <c>WHERE</c> clause can use. They cannot be shared: the first runs over a materialised
/// row in memory and the second has to reach PostgreSQL as an expression tree.
/// <c>TicketSlaFilterTests</c> is what keeps them honest — it seeds a ticket in every
/// state, asks the database for each one, and asserts the answer matches what
/// <see cref="SlaAssessment"/> says about the same rows. A change to either rule that is
/// not made to both fails there.
/// </para>
/// <para>
/// <b>The resolution clock, not the response clock.</b> "Overdue" means the fix is late,
/// which is the question a queue is triaged on; a response filter would be a second
/// parameter and nothing has asked for one. It is named <c>slaState</c> rather than
/// <c>overdue</c> so the other four states are reachable without a second flag being added
/// the first time somebody wants "at risk".
/// </para>
/// </remarks>
internal static class TicketSlaFilter
{
    /// <summary>
    /// Keeps only the tickets whose resolution clock reads <paramref name="state"/> at
    /// <paramref name="now"/>.
    /// </summary>
    /// <remarks>
    /// A paused clock is judged at the instant it was paused —
    /// <c>COALESCE(sla_paused_at, now)</c> — for the reason
    /// <see cref="SlaAssessment.Of"/> gives: the stored deadline is frozen for the length
    /// of the pause, so comparing it against a moving <c>now</c> would let a ticket drift
    /// into a breach by sitting in Waiting.
    /// </remarks>
    /// <param name="tickets">The query so far.</param>
    /// <param name="state">The state wanted.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <returns>The narrowed query. Nothing has been executed.</returns>
    public static IQueryable<Ticket> WithSlaState(
        this IQueryable<Ticket> tickets,
        SlaState state,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(tickets);

        return state switch
        {
            // A cancelled ticket has no outcome, and no other ticket is Stopped.
            SlaState.Stopped => tickets.Where(ticket => ticket.Status == TicketStatus.Cancelled),

            SlaState.Met => tickets.Where(ticket =>
                ticket.Status != TicketStatus.Cancelled
                && ticket.ResolvedAt != null
                && ticket.ResolvedAt <= ticket.DueAt),

            // Stopped after the deadline, or still running past it. The first comparison is
            // strict and the second is not — see SlaClock for why the two boundaries differ.
            SlaState.Breached => tickets.Where(ticket =>
                ticket.Status != TicketStatus.Cancelled
                && ((ticket.ResolvedAt != null && ticket.ResolvedAt > ticket.DueAt)
                    || (ticket.ResolvedAt == null && (ticket.SlaPausedAt ?? now) >= ticket.DueAt))),

            SlaState.Approaching => tickets.Where(ticket =>
                ticket.Status != TicketStatus.Cancelled
                && ticket.ResolvedAt == null
                && (ticket.SlaPausedAt ?? now) >= ticket.ResolutionWarnAt
                && (ticket.SlaPausedAt ?? now) < ticket.DueAt),

            _ => tickets.Where(ticket =>
                ticket.Status != TicketStatus.Cancelled
                && ticket.ResolvedAt == null
                && (ticket.SlaPausedAt ?? now) < ticket.ResolutionWarnAt),
        };
    }
}
