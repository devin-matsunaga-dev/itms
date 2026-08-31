using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.Tickets;

/// <summary>What a ticket looks like immediately after its assignee changed.</summary>
/// <remarks>
/// Deliberately not the whole ticket, following <see cref="TicketStatusChangeResponse"/>:
/// it says only what the assignment did. Both the previous holder and the new one are
/// carried because a queue screen has to move the row out of one person's list and into
/// another's without re-reading it.
/// </remarks>
/// <param name="Id">The ticket.</param>
/// <param name="Number">Its human-readable number, so a toast can name it.</param>
/// <param name="PreviousAssigneeId">Who held it before, or <see langword="null"/> if nobody did.</param>
/// <param name="PreviousAssigneeName">Their display name as the ticket had cached it, or <see langword="null"/>.</param>
/// <param name="AssigneeId">Who holds it now, or <see langword="null"/> after an unassignment.</param>
/// <param name="AssigneeName">Their display name, cached on the row at this moment.</param>
/// <param name="PreviousStatus">The status before the assignment.</param>
/// <param name="Status">
/// The status now. It differs from <paramref name="PreviousStatus"/> only on a first
/// assignment (<c>New → Assigned</c>) and on an unassignment (<c>Assigned → New</c>);
/// reassignment leaves the workflow exactly where it was.
/// </param>
/// <param name="ChangedAt">When the assignment happened, in UTC.</param>
/// <param name="AllowedNextStatuses">
/// Where the ticket may go next, straight from the state machine, for the same reason
/// <see cref="TicketStatusChangeResponse"/> carries it: WP-1.10 must not render a
/// transition the server would refuse.
/// </param>
public sealed record TicketAssignmentResponse(
    Guid Id,
    string Number,
    Guid? PreviousAssigneeId,
    string? PreviousAssigneeName,
    Guid? AssigneeId,
    string? AssigneeName,
    TicketStatus PreviousStatus,
    TicketStatus Status,
    DateTimeOffset ChangedAt,
    IReadOnlyCollection<TicketStatus> AllowedNextStatuses);

/// <summary>An assignment together with the row version the ticket carries after it.</summary>
/// <remarks>
/// Internal and never serialised, for the reason <see cref="TicketDetail"/> gives: the
/// version is a fact about the write, and it travels as an <c>ETag</c> header rather than
/// in the body.
/// </remarks>
/// <param name="Response">The assignment as the client sees it.</param>
/// <param name="Version">The <c>xmin</c> row version after the write.</param>
internal sealed record TicketAssignment(TicketAssignmentResponse Response, uint Version);
