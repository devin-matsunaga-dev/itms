using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Identity;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.Tickets.ListTickets;

/// <summary>Reads the ticket queue.</summary>
/// <remarks>
/// <para>
/// <b>Projected, never loaded.</b> CONVENTIONS.md forbids loading an aggregate to render a
/// list, and WP-1.5's own criterion is fifty thousand tickets under 200 ms. The query
/// selects straight into <see cref="TicketListItemResponse"/> with no tracking, no lazy
/// loading, and no navigation property to walk — WP-1.2 deliberately declared none.
/// </para>
/// <para>
/// <b>The scope comes first.</b> <see cref="TicketScope.VisibleTo"/> is applied before any
/// filter, so a User's every query — however they shape it — is already narrowed to their
/// own tickets. A filter cannot widen it back.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="currentUser">Who is asking. Decides how much of the queue exists.</param>
internal sealed class ListTicketsHandler(HelpdeskDbContext database, ICurrentUser currentUser)
{
    /// <summary>Reads a page of the queue.</summary>
    /// <param name="query">The filters and the ordering.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The page envelope. An empty page is a success, never a 404.</returns>
    public async Task<Result<PagedResult<TicketListItemResponse>>> HandleAsync(
        ListTicketsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = PageRequest.Of(query.Page, query.PageSize);

        var tickets = Filter(database.Tickets.AsNoTracking().VisibleTo(currentUser), query);

        var total = await tickets.CountAsync(cancellationToken).ConfigureAwait(false);

        if (total == 0)
        {
            // Nothing matched. Skipping the second round trip is worth the branch on a
            // screen whose empty state is a first-run certainty.
            return PagedResult.Empty<TicketListItemResponse>(page);
        }

        // Joined to the reference data, ordered, paged, and only then projected — in that
        // order, and all in one SQL statement.
        //
        // The shape carrying the join is anonymous rather than a named record because EF
        // sees through an anonymous type in a join and cannot see through a record's
        // positional constructor: ordering by `row.Ticket.CreatedAt` over a named record
        // fails to translate outright. That is also why the ordering is applied here rather
        // than after the projection — an OrderBy written over a constructed
        // TicketListItemResponse has the same problem, and the priority sort needs the
        // priority's rank, which is not a column on the ticket at all.
        var rows =
            from ticket in tickets
            join category in database.TicketCategories on ticket.CategoryId equals category.Id
            join priority in database.TicketPriorities on ticket.PriorityId equals priority.Id
            select new { Ticket = ticket, Category = category, Priority = priority };

        var sort = query.Sort ?? TicketSort.CreatedAt;

        // Priority and due date are asked for because the urgent end is wanted first, so
        // ascending is their useful default. Everything else means "most recent first".
        var descending = query.Direction switch
        {
            SortDirection.Ascending => false,
            SortDirection.Descending => true,
            _ => sort is not (TicketSort.Priority or TicketSort.DueAt),
        };

        // Every ordering ends at the id. None of the sort columns is unique — two tickets
        // share a priority, a due date, even a creation instant under a fast enough clock —
        // and a paged list whose order changes between two reads of the same data silently
        // drops and duplicates rows across page boundaries. WP-1.4 learned that from a test
        // rather than from reasoning; the tiebreaker is not optional.
        var ordered = sort switch
        {
            TicketSort.Priority => descending
                ? rows.OrderByDescending(r => r.Priority.Rank)
                      .ThenBy(r => r.Ticket.CreatedAt).ThenBy(r => r.Ticket.Id)
                : rows.OrderBy(r => r.Priority.Rank)
                      .ThenBy(r => r.Ticket.CreatedAt).ThenBy(r => r.Ticket.Id),

            TicketSort.UpdatedAt => descending
                ? rows.OrderByDescending(r => r.Ticket.UpdatedAt).ThenByDescending(r => r.Ticket.Id)
                : rows.OrderBy(r => r.Ticket.UpdatedAt).ThenBy(r => r.Ticket.Id),

            TicketSort.Number => descending
                ? rows.OrderByDescending(r => r.Ticket.Number).ThenByDescending(r => r.Ticket.Id)
                : rows.OrderBy(r => r.Ticket.Number).ThenBy(r => r.Ticket.Id),

            // Nulls last in both directions: a ticket with no due date is not the most urgent
            // thing in the queue, and PostgreSQL would sort it first descending.
            TicketSort.DueAt => descending
                ? rows.OrderBy(r => r.Ticket.DueAt == null)
                      .ThenByDescending(r => r.Ticket.DueAt).ThenByDescending(r => r.Ticket.Id)
                : rows.OrderBy(r => r.Ticket.DueAt == null)
                      .ThenBy(r => r.Ticket.DueAt).ThenBy(r => r.Ticket.Id),

            _ => descending
                ? rows.OrderByDescending(r => r.Ticket.CreatedAt).ThenByDescending(r => r.Ticket.Id)
                : rows.OrderBy(r => r.Ticket.CreatedAt).ThenBy(r => r.Ticket.Id),
        };

        var items = await ordered
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(r => new TicketListItemResponse(
                r.Ticket.Id,
                r.Ticket.Number,
                r.Ticket.Subject,
                r.Ticket.Status,
                r.Category.Id,
                r.Category.Name,
                r.Priority.Id,
                r.Priority.Name,
                r.Priority.Code,
                r.Priority.Rank,
                r.Ticket.RequesterId,
                r.Ticket.RequesterName,
                r.Ticket.DepartmentId,
                r.Ticket.DepartmentName,
                r.Ticket.AssigneeId,
                r.Ticket.AssigneeName,
                r.Ticket.CreatedAt,
                r.Ticket.UpdatedAt,
                r.Ticket.DueAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.From<TicketListItemResponse>(items, total, page);
    }

    /// <summary>Applies the filters that are actually present.</summary>
    /// <remarks>
    /// Each is skipped when null rather than folded into one expression with null checks
    /// inside it, because a <c>WHERE (@p IS NULL OR col = @p)</c> is the shape that makes
    /// PostgreSQL choose a plan for the parameter it was first given and then keep it for
    /// every other combination.
    /// </remarks>
    private static IQueryable<Ticket> Filter(IQueryable<Ticket> tickets, ListTicketsQuery query)
    {
        if (query.Status is { Length: > 0 } statuses)
        {
            // Distinct, because a repeated value in the query string would otherwise reach
            // the database as a longer IN list saying the same thing.
            var wanted = statuses.Distinct().ToArray();

            tickets = wanted.Length == 1
                ? tickets.Where(ticket => ticket.Status == wanted[0])
                : tickets.Where(ticket => wanted.Contains(ticket.Status));
        }

        if (query.PriorityId is { } priorityId)
        {
            tickets = tickets.Where(ticket => ticket.PriorityId == priorityId);
        }

        if (query.CategoryId is { } categoryId)
        {
            tickets = tickets.Where(ticket => ticket.CategoryId == categoryId);
        }

        if (query.Unassigned is true)
        {
            // Wins over AssigneeId: asking for both is a contradiction, and answering the
            // narrower of the two is the safe reading.
            tickets = tickets.Where(ticket => ticket.AssigneeId == null);
        }
        else if (query.AssigneeId is { } assigneeId)
        {
            tickets = tickets.Where(ticket => ticket.AssigneeId == assigneeId);
        }

        if (query.DepartmentId is { } departmentId)
        {
            tickets = tickets.Where(ticket => ticket.DepartmentId == departmentId);
        }

        if (query.RequesterId is { } requesterId)
        {
            tickets = tickets.Where(ticket => ticket.RequesterId == requesterId);
        }

        if (query.CreatedFrom is { } from)
        {
            tickets = tickets.Where(ticket => ticket.CreatedAt >= from);
        }

        if (query.CreatedTo is { } to)
        {
            tickets = tickets.Where(ticket => ticket.CreatedAt <= to);
        }

        return tickets;
    }
}
