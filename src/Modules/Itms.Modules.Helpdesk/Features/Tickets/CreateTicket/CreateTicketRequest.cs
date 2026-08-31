namespace Itms.Modules.Helpdesk.Features.Tickets.CreateTicket;

/// <summary>Raises a ticket.</summary>
/// <remarks>
/// <para>
/// <b>There is no status field.</b> Every ticket starts at <c>New</c> and moves through
/// WP-1.3's state machine; letting a caller name a starting state would be the first way
/// around it. There is no assignee field either — assignment is WP-1.6, and it is a
/// transition, not a property of creation.
/// </para>
/// <para>
/// <b><see cref="RequesterId"/> and <see cref="DepartmentId"/> are optional, and they are
/// optional for different reasons.</b> The requester defaults to the caller, because the
/// commonest ticket in the system is somebody filing their own; a <b>User</b> supplying
/// anybody else's id is refused with 403 rather than quietly corrected. The department
/// defaults to whatever the requester's account names, because an end user should not have
/// to answer a question their own record already answers — but the column is
/// <c>NOT NULL</c>, so if neither is present the request fails.
/// </para>
/// </remarks>
/// <param name="Subject">The one-line summary. SPEC.md §2 calls this the title, and a form should label it so.</param>
/// <param name="Description">What is wrong, in the requester's words.</param>
/// <param name="CategoryId">What the ticket is about. Must name a category that has not been retired.</param>
/// <param name="PriorityId">How urgent it is. Must name a priority that has not been retired.</param>
/// <param name="RequesterId">
/// Who the ticket is for, or <see langword="null"/> for the caller. Only a Technician or
/// an Admin may name somebody else.
/// </param>
/// <param name="DepartmentId">
/// The department to file it against, or <see langword="null"/> to take the requester's
/// own.
/// </param>
public sealed record CreateTicketRequest(
    string Subject,
    string Description,
    Guid CategoryId,
    Guid PriorityId,
    Guid? RequesterId = null,
    Guid? DepartmentId = null);
