using Itms.Modules.Helpdesk.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Itms.Modules.Helpdesk.Features.Tickets.ListTickets;

/// <summary>
/// Everything the ticket queue can be narrowed and ordered by, bound from the query
/// string.
/// </summary>
/// <remarks>
/// <para>
/// One type rather than fourteen parameters on the endpoint delegate, so the filter set is
/// a thing with a name that OpenAPI describes and the generated client can hold. Every
/// member is optional: no filters at all is the whole queue, newest first.
/// </para>
/// <para>
/// <b>CONVENTIONS.md requires every list screen to keep its filters in the URL.</b> That is
/// the client's obligation, and this is the shape that makes it possible — every filter is
/// a scalar or a repeated scalar, so a view is a link somebody can paste to a colleague.
/// Nothing here is a body, and nothing here is stateful.
/// </para>
/// </remarks>
public sealed class ListTicketsQuery
{
    /// <summary>
    /// The statuses to include. Repeat the parameter for several
    /// (<c>?status=New&amp;status=Assigned</c>); omit it for every status.
    /// </summary>
    /// <remarks>
    /// Repeatable because "open" is not a status — it is four of them — and every queue
    /// view in SPEC.md §1 is a set rather than one value. The other filters are single
    /// because nothing has yet asked to see two departments at once.
    /// </remarks>
    [FromQuery(Name = "status")]
    public TicketStatus[]? Status { get; init; }

    /// <summary>Only tickets at this priority.</summary>
    [FromQuery(Name = "priorityId")]
    public Guid? PriorityId { get; init; }

    /// <summary>Only tickets in this category.</summary>
    [FromQuery(Name = "categoryId")]
    public Guid? CategoryId { get; init; }

    /// <summary>Only tickets held by this technician.</summary>
    /// <remarks>Ignored when <see cref="Unassigned"/> is true — nobody holds an unassigned ticket.</remarks>
    [FromQuery(Name = "assigneeId")]
    public Guid? AssigneeId { get; init; }

    /// <summary>Only tickets nobody holds. The "Unassigned" view WP-1.9 names.</summary>
    /// <remarks>
    /// A flag of its own rather than an absent <see cref="AssigneeId"/>, because "no filter
    /// on the assignee" and "filter to those with no assignee" are different questions and
    /// a null cannot mean both.
    /// </remarks>
    [FromQuery(Name = "unassigned")]
    public bool? Unassigned { get; init; }

    /// <summary>Only tickets filed against this department.</summary>
    [FromQuery(Name = "departmentId")]
    public Guid? DepartmentId { get; init; }

    /// <summary>Only tickets raised for this person.</summary>
    /// <remarks>
    /// A <b>User</b> is already narrowed to their own tickets before this applies, so
    /// setting it to somebody else returns nothing rather than somebody else's queue —
    /// see <see cref="TicketScope"/>.
    /// </remarks>
    [FromQuery(Name = "requesterId")]
    public Guid? RequesterId { get; init; }

    /// <summary>Only tickets raised at or after this instant (UTC).</summary>
    [FromQuery(Name = "createdFrom")]
    public DateTimeOffset? CreatedFrom { get; init; }

    /// <summary>Only tickets raised at or before this instant (UTC).</summary>
    /// <remarks>
    /// Inclusive, so a caller passing the end of a day gets that day. A range whose end is
    /// before its start is not an error — it is a filter that matches nothing, which is
    /// what it says.
    /// </remarks>
    [FromQuery(Name = "createdTo")]
    public DateTimeOffset? CreatedTo { get; init; }

    /// <summary>What to order by. Defaults to <see cref="TicketSort.CreatedAt"/>.</summary>
    [FromQuery(Name = "sort")]
    public TicketSort? Sort { get; init; }

    /// <summary>
    /// Which way to order. Defaults to <see cref="SortDirection.Descending"/> for every
    /// sort but <see cref="TicketSort.Priority"/> and <see cref="TicketSort.DueAt"/>, where
    /// the useful end is the front.
    /// </summary>
    [FromQuery(Name = "direction")]
    public SortDirection? Direction { get; init; }

    /// <summary>The 1-based page number. Out-of-range values are clamped, not rejected.</summary>
    [FromQuery(Name = "page")]
    public int? Page { get; init; }

    /// <summary>How many tickets to a page, up to the API-wide maximum of 200.</summary>
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; }
}
