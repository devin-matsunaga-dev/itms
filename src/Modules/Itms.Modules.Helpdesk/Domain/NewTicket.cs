namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// Everything a ticket must have the moment it exists.
/// </summary>
/// <remarks>
/// <para>
/// Invariant 1 says a ticket always has a requester, a category, a priority, and a
/// status. This record is that invariant written as a shape: a caller cannot reach
/// <see cref="Ticket.Create"/> having forgotten one, because there is nowhere to put a
/// ticket that is missing it. The status is not here — it is not the caller's to choose
/// (see <see cref="Ticket.Create"/>).
/// </para>
/// <para>
/// The requester's and the department's display names travel with their ids because
/// ARCHITECTURE.md §3 rule 6 forbids a foreign key across a module boundary and requires
/// an id plus a cached display string instead. The caller reads them from
/// <c>IUserLookup</c> and <c>IDepartmentLookup</c>; the category and the priority are
/// Helpdesk's own rows, so they are ids alone and a rename reaches every ticket without
/// anything being copied.
/// </para>
/// </remarks>
/// <param name="Subject">The one-line summary. SPEC.md §2 calls this the title; the domain event settled on subject.</param>
/// <param name="Description">What the requester reported.</param>
/// <param name="RequesterId">Who the ticket is for.</param>
/// <param name="RequesterName">Their display name at creation, cached per §3 rule 6.</param>
/// <param name="DepartmentId">The department it is filed against.</param>
/// <param name="DepartmentName">That department's name at creation, cached per §3 rule 6.</param>
/// <param name="CategoryId">The category, a row in this module's own table.</param>
/// <param name="PriorityId">The priority, a row in this module's own table.</param>
public sealed record NewTicket(
    string Subject,
    string Description,
    Guid RequesterId,
    string RequesterName,
    Guid DepartmentId,
    string DepartmentName,
    Guid CategoryId,
    Guid PriorityId);
