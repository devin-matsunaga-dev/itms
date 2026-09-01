using Itms.Modules.Helpdesk.Domain;
using Itms.Platform.Paging;
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

    /// <summary>
    /// Only tickets whose <em>resolution</em> clock reads this state. The "Overdue" view
    /// WP-1.9 names is <c>?slaState=Breached</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The state is computed, not stored, so this is a comparison against the server's
    /// clock at the moment of the request — see <c>TicketSlaFilter</c>. A ticket parked in
    /// Waiting is judged at the instant it was parked, which is what stops it drifting into
    /// the overdue view while nobody can work on it.
    /// </para>
    /// <para>
    /// Single rather than repeatable, unlike <see cref="Status"/>: the five states are
    /// mutually exclusive and no view has yet wanted two of them at once.
    /// </para>
    /// </remarks>
    [FromQuery(Name = "slaState")]
    public SlaState? SlaState { get; init; }

    /// <summary>
    /// Free text matched against the ticket number, the subject, and the requester's
    /// cached name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately narrow. It is a case-insensitive "contains" over three columns of one
    /// table — not the cross-entity search <c>WP-4.2</c> builds, which spans users, assets,
    /// hostnames and serials on PostgreSQL full-text plus trigram. Nothing here enables an
    /// extension or adds an index; a queue search is a filter, and the global search is a
    /// different feature with a different shape.
    /// </para>
    /// <para>
    /// <b>The description is not searched</b>, at the human's direction: it is an
    /// eight-thousand-character column and including it would make every keystroke a full
    /// scan of every ticket body. The requester's name is the <em>cached</em> one on the
    /// ticket row (§3 rule 6), so a person renamed since they raised a ticket is found by
    /// the name the ticket carries — the same name the queue is displaying.
    /// </para>
    /// <para>
    /// Applied after <c>TicketScope</c>, never before. A User searching finds only among
    /// the tickets they raised.
    /// </para>
    /// </remarks>
    [FromQuery(Name = "search")]
    public string? Search { get; init; }

    /// <summary>
    /// Only tickets still open whose resolution is due before this instant — the "Due
    /// today" counter's list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The caller supplies the instant rather than the server deciding what "today" means,
    /// because the day boundary somebody means is the one on their own clock and the wire
    /// is UTC (ARCHITECTURE.md §11). It is the same call WP-1.9 made for the created-date
    /// range.
    /// </para>
    /// <para>
    /// "Still open" is part of the filter and not a separate one: a ticket resolved
    /// yesterday is not due today, whatever its deadline column says. Resolved, Closed, and
    /// Cancelled are all excluded.
    /// </para>
    /// </remarks>
    [FromQuery(Name = "dueBefore")]
    public DateTimeOffset? DueBefore { get; init; }

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
