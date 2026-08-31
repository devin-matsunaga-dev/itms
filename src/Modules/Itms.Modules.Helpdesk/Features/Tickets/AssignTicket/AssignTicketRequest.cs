namespace Itms.Modules.Helpdesk.Features.Tickets.AssignTicket;

/// <summary>The body of <c>POST /api/v1/tickets/{id}/assignments</c>.</summary>
/// <remarks>
/// <para>
/// One route and one body cover assigning, reassigning, and unassigning, because all
/// three are the same fact — who holds the ticket now — and all three raise one
/// <c>TicketAssigned</c> event and write one history line. Splitting unassignment onto a
/// <c>DELETE</c> of its own would have made two routes, two handlers, and two chances for
/// the history to be written differently.
/// </para>
/// <para>
/// Posted as a resource rather than patched onto the ticket, following the status change:
/// an assignment is an event in the ticket's life that WP-1.4 records a row per, and a
/// refused one has to be distinguishable from a field that was not sent.
/// </para>
/// </remarks>
/// <param name="AssigneeId">
/// The technician taking the ticket on, or <see langword="null"/> to unassign it. A null
/// is a deliberate instruction, not an omitted field: unassigning returns the ticket to
/// <c>New</c>, and is refused once work has started.
/// </param>
public sealed record AssignTicketRequest(Guid? AssigneeId);
