namespace Itms.Modules.Helpdesk.Features.Tickets.TicketCounters;

/// <summary>
/// The queue's headline numbers: what the KPI row shows and what the saved views count.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every count is scope-wide and ignores the caller's current filters</b>, at the
/// human's direction. A counter that moved when somebody narrowed the queue would be
/// describing their filter rather than their queue, and the KPI row's job is to say what
/// is waiting regardless of what is on screen. "Scope-wide" still means
/// <c>TicketScope</c>-wide: a User's numbers count only the tickets they raised.
/// </para>
/// <para>
/// <b>There are no deltas here</b>, and the mockup's "↑12% vs last week" is deliberately
/// unbuilt — see the remarks on <see cref="TicketCountersHandler"/>. Adding them is a
/// schema question, not an arithmetic one.
/// </para>
/// </remarks>
/// <param name="All">Every ticket the caller can see, in any status.</param>
/// <param name="Open">Tickets still being worked: New, Assigned, In Progress, or Waiting.</param>
/// <param name="Unassigned">Open tickets nobody holds.</param>
/// <param name="Overdue">Tickets whose resolution clock has breached.</param>
/// <param name="DueToday">
/// Open tickets due before the instant the caller named as the end of their day.
/// </param>
/// <param name="Mine">
/// The caller's own workload — tickets assigned to them if they work the queue, or
/// tickets they raised if they do not. The same rule WP-1.9's "My tickets" view follows,
/// so the chip and its count mean one thing.
/// </param>
public sealed record TicketCountersResponse(
    int All,
    int Open,
    int Unassigned,
    int Overdue,
    int DueToday,
    int Mine);
