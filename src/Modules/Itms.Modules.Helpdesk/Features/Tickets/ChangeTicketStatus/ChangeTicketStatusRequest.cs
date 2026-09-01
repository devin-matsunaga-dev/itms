using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.Tickets.ChangeTicketStatus;

/// <summary>The body of <c>POST /api/v1/tickets/{id}/status-changes</c>.</summary>
/// <remarks>
/// <para>
/// A status change is posted as a resource rather than patched onto the ticket, because it
/// is an event in the ticket's life: WP-1.4 records one history row per post, and a
/// transition that is refused has to be distinguishable from a field that was not sent.
/// The route is the one CONVENTIONS.md uses as its own example.
/// </para>
/// <para>
/// <see cref="Status"/> is the destination, not the transition. Which move that is — start,
/// resume, reopen — follows from the ticket's current state, and the client does not have
/// to work it out.
/// </para>
/// </remarks>
/// <param name="Status">
/// The status to move to. <c>Assigned</c> is not accepted here: a ticket becomes assigned
/// by assigning it to somebody (WP-1.6), so that the status and the assignee arrive
/// together. <c>New</c> is not accepted either — nothing returns to it.
/// </param>
/// <param name="ResolutionNotes">
/// What was done. Required and non-blank when <paramref name="Status"/> is
/// <c>Resolved</c>, and rejected for every other destination, because no other transition
/// records a resolution and silently dropping the text would lose it.
/// </param>
/// <param name="HoldReason">
/// What the ticket is waiting on. Required and non-blank when <paramref name="Status"/> is
/// <c>Waiting</c>, and rejected for every other destination — the exact mirror of
/// <paramref name="ResolutionNotes"/>, and for the same reason: a ticket nobody is working
/// should say why, and text somebody typed should never be silently dropped.
///
/// It is kept only while the ticket is parked. Resuming clears it, which is what makes a
/// second hold write its own line in the timeline rather than looking like no change at
/// all. Every reason ever given stays in the ticket's history.
/// </param>
public sealed record ChangeTicketStatusRequest(
    TicketStatus Status,
    string? ResolutionNotes,
    string? HoldReason);
