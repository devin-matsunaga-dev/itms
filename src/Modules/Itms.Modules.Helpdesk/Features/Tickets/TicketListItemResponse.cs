using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.Tickets;

/// <summary>One row of the ticket queue, as the API renders it.</summary>
/// <remarks>
/// <para>
/// Every field a queue screen draws is here, so the list is one round trip and never a
/// row followed by a lookup per row. That is why the category and priority names are
/// joined in rather than resolved afterwards, and why the requester, department, and
/// assignee names are read from the ticket's own cached columns (§3 rule 6) rather than
/// from Identity and Directory.
/// </para>
/// <para>
/// <b>The priority carries its code as well as its name.</b> DESIGN.md §2 fixes a colour
/// per priority and WP-1.1 gave a priority an immutable code precisely so a rename cannot
/// move that key. A client colours on <see cref="PriorityCode"/> and labels on
/// <see cref="PriorityName"/>; colouring on the name would break the first time somebody
/// renames "Critical" to "P1".
/// </para>
/// </remarks>
/// <param name="Id">The ticket's id.</param>
/// <param name="Number">The human-readable number, <c>TKT-####</c>.</param>
/// <param name="Subject">The one-line summary. SPEC.md §2 calls this the title, and a UI should label it so.</param>
/// <param name="Status">Where it sits in the workflow.</param>
/// <param name="CategoryId">What it is about.</param>
/// <param name="CategoryName">That category's name, as it reads now.</param>
/// <param name="PriorityId">How urgent it is.</param>
/// <param name="PriorityName">That priority's name, as it reads now.</param>
/// <param name="PriorityCode">That priority's stable key, for colour and for rules.</param>
/// <param name="PriorityRank">Its ordering weight, so a client can sort a page it already holds.</param>
/// <param name="RequesterId">Who the ticket is for.</param>
/// <param name="RequesterName">Their display name, cached at creation.</param>
/// <param name="DepartmentId">The department it is filed against.</param>
/// <param name="DepartmentName">That department's name, cached at creation.</param>
/// <param name="AssigneeId">The technician responsible, or <see langword="null"/> while unassigned.</param>
/// <param name="AssigneeName">Their display name, or <see langword="null"/>.</param>
/// <param name="CreatedAt">When it was raised (UTC). The queue's "age" column is computed from this.</param>
/// <param name="UpdatedAt">When it last moved (UTC).</param>
/// <param name="DueAt">When resolution is due, or <see langword="null"/> until WP-1.8 computes it.</param>
public sealed record TicketListItemResponse(
    Guid Id,
    string Number,
    string Subject,
    TicketStatus Status,
    Guid CategoryId,
    string CategoryName,
    Guid PriorityId,
    string PriorityName,
    string PriorityCode,
    int PriorityRank,
    Guid RequesterId,
    string RequesterName,
    Guid DepartmentId,
    string DepartmentName,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DueAt)
{
    // No projection expression here, deliberately. A list has to be ordered before it is
    // projected — EF cannot translate an OrderBy written over a constructed record, and the
    // priority sort orders by the priority's rank, which is not a column on the ticket — so
    // ListTicketsHandler joins, orders, pages, and only then builds this shape. It is the
    // only query that produces one.
}
