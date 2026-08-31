namespace Itms.Contracts.Events;

/// <summary>
/// A ticket was created. Notifications, the search index, and the audit trail all
/// build from this rather than from Helpdesk calling into them.
/// </summary>
/// <param name="TicketId">The new ticket.</param>
/// <param name="TicketNumber">The human-readable number (<c>TKT-####</c>), carried so consumers need no lookup to render it.</param>
/// <param name="RequesterId">The user the ticket was raised for.</param>
/// <param name="CategoryId">The category at creation.</param>
/// <param name="Priority">The priority at creation.</param>
/// <param name="Subject">The subject line, for notification and search text.</param>
public sealed record TicketCreated(
    Guid TicketId,
    string TicketNumber,
    Guid RequesterId,
    Guid CategoryId,
    string Priority,
    string Subject) : DomainEvent;

/// <summary>
/// A ticket's assignee changed, including being unassigned.
/// </summary>
/// <param name="TicketId">The ticket.</param>
/// <param name="TicketNumber">The human-readable number.</param>
/// <param name="AssigneeId">The technician now responsible, or <see langword="null"/> when the ticket was unassigned.</param>
/// <param name="PreviousAssigneeId">Who held it before, so a consumer can notify the person who lost it.</param>
public sealed record TicketAssigned(
    Guid TicketId,
    string TicketNumber,
    Guid? AssigneeId,
    Guid? PreviousAssigneeId) : DomainEvent;

/// <summary>
/// A ticket moved through the state machine in SPEC.md §2. Both states are carried
/// because the audit diff and the SLA clock both need the transition, not just the
/// destination.
/// </summary>
/// <param name="TicketId">The ticket.</param>
/// <param name="TicketNumber">The human-readable number.</param>
/// <param name="FromStatus">The status before the transition.</param>
/// <param name="ToStatus">The status after the transition.</param>
public sealed record TicketStatusChanged(
    Guid TicketId,
    string TicketNumber,
    string FromStatus,
    string ToStatus) : DomainEvent;

/// <summary>
/// A ticket was resolved. Separate from <see cref="TicketStatusChanged"/> because
/// resolution stops the SLA clock and triggers the requester notification, and a
/// consumer of that should not have to string-match a status name to find out.
/// </summary>
/// <param name="TicketId">The ticket.</param>
/// <param name="TicketNumber">The human-readable number.</param>
/// <param name="RequesterId">Who to tell.</param>
/// <param name="ResolvedAt">When resolution happened, in UTC.</param>
/// <param name="ResolutionSummary">The resolution text, for the notification and the knowledge base suggestion.</param>
public sealed record TicketResolved(
    Guid TicketId,
    string TicketNumber,
    Guid RequesterId,
    DateTimeOffset ResolvedAt,
    string ResolutionSummary) : DomainEvent;
