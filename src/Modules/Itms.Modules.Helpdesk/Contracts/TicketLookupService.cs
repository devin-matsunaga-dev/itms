using Itms.Contracts.Lookups;
using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Paging;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Contracts;

/// <summary>
/// Helpdesk's half of <see cref="ITicketLookup"/> — the only way another module reads a
/// ticket (ARCHITECTURE.md §3 rule 2).
/// </summary>
/// <remarks>
/// <para>
/// Every query goes through the one <see cref="Project"/>, so no method here can widen
/// what leaves the module. What is absent is the point: no description, no
/// resolution notes, no comments. An internal note is the one thing a requester may never
/// read, and the cheapest way to guarantee that across a boundary is for the conversation
/// never to be in the shape that crosses it.
/// </para>
/// <para>
/// <b>Nothing here applies <c>TicketScope</c>.</b> The row-level rule guards Helpdesk's
/// own endpoints, where the caller is asking for tickets; a consumer of this interface is
/// asking about a person or an asset it has already decided the caller may see, and
/// narrowing again here would empty a technician's panel of somebody else's history —
/// which is exactly what the panel is for. The interface's own remarks say so, and the
/// route that exposes one of these owns its authorization.
/// </para>
/// <para>
/// The soft-delete filter does apply, through the context's global filter, so a deleted
/// ticket is invisible here as well.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
internal sealed class TicketLookupService(HelpdeskDbContext database) : ITicketLookup
{
    /// <inheritdoc />
    public async Task<TicketSummary?> GetAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var row = await Project(database.Tickets.AsNoTracking().Where(ticket => ticket.Id == ticketId))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : Summarize(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TicketSummary>> GetManyAsync(
        IReadOnlyCollection<Guid> ticketIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticketIds);

        if (ticketIds.Count == 0)
        {
            return [];
        }

        var ids = ticketIds.Distinct().ToArray();

        var rows = await Project(
                database.Tickets
                    .AsNoTracking()
                    .Where(ticket => ids.Contains(ticket.Id))
                    .OrderByDescending(ticket => ticket.CreatedAt)
                    .ThenByDescending(ticket => ticket.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(Summarize)];
    }

    /// <inheritdoc />
    public Task<TicketPage> GetForRequesterAsync(
        Guid requesterId,
        TicketActivity activity,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        PageAsync(
            Narrow(database.Tickets.AsNoTracking().Where(ticket => ticket.RequesterId == requesterId), activity),
            page,
            pageSize,
            cancellationToken);

    /// <inheritdoc />
    public Task<TicketPage> GetForAssetAsync(
        Guid assetId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        PageAsync(
            database.Tickets.AsNoTracking().Where(ticket => ticket.RelatedAssetId == assetId),
            page,
            pageSize,
            cancellationToken);

    /// <summary>
    /// Narrows a ticket query to one half of somebody's history.
    /// </summary>
    /// <remarks>
    /// Written against <see cref="TicketStateMachine.IsTerminal"/>'s meaning rather than
    /// calling it, because the predicate has to reach the database: a ticket is open until
    /// it is resolved, closed, or cancelled. The two sets are complementary, so a caller
    /// asking for both halves sees every ticket exactly once.
    /// </remarks>
    /// <param name="tickets">The query so far.</param>
    /// <param name="activity">Which half is wanted.</param>
    /// <returns>The query, narrowed if it needs to be.</returns>
    private static IQueryable<Ticket> Narrow(IQueryable<Ticket> tickets, TicketActivity activity) =>
        activity switch
        {
            TicketActivity.Open => tickets.Where(ticket =>
                ticket.Status != TicketStatus.Resolved
                && ticket.Status != TicketStatus.Closed
                && ticket.Status != TicketStatus.Cancelled),
            TicketActivity.Past => tickets.Where(ticket =>
                ticket.Status == TicketStatus.Resolved
                || ticket.Status == TicketStatus.Closed
                || ticket.Status == TicketStatus.Cancelled),
            _ => tickets,
        };

    /// <summary>
    /// Counts, orders, pages, and projects — the shape both paged reads share.
    /// </summary>
    /// <remarks>
    /// Newest first, with the id as a tiebreaker so two tickets raised in the same instant
    /// cannot swap places between two reads of the same page. The page is clamped through
    /// <see cref="PageRequest"/> rather than trusted, because a caller of a contract is
    /// another module's code and a page size of <c>int.MaxValue</c> would be a table scan.
    /// </remarks>
    private async Task<TicketPage> PageAsync(
        IQueryable<Ticket> tickets,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var request = PageRequest.Of(page, pageSize);
        var total = await tickets.CountAsync(cancellationToken).ConfigureAwait(false);

        if (total == 0)
        {
            return new TicketPage([], 0, request.Page, request.PageSize);
        }

        var rows = await Project(
                tickets
                    .OrderByDescending(ticket => ticket.CreatedAt)
                    .ThenByDescending(ticket => ticket.Id)
                    .Skip(request.Skip)
                    .Take(request.Take))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TicketPage([.. rows.Select(Summarize)], total, request.Page, request.PageSize);
    }

    /// <summary>
    /// Whether a ticket in <paramref name="status"/> is still being worked.
    /// </summary>
    /// <remarks>
    /// The one definition of "open" this module hands out, so <see cref="Narrow"/>'s
    /// database predicate and <see cref="TicketSummary.IsOpen"/> cannot disagree — a page
    /// of open tickets containing a row flagged closed is the kind of contradiction a
    /// consumer has no way to resolve. It is broader than
    /// <c>TicketStateMachine.IsTerminal</c> on purpose: a resolved ticket can still be
    /// reopened, but it is not work in hand, and SPEC.md §4's user page puts it under
    /// "previous tickets".
    /// </remarks>
    /// <param name="status">The status to judge.</param>
    private static bool IsOpen(TicketStatus status) =>
        status is not (TicketStatus.Resolved or TicketStatus.Closed or TicketStatus.Cancelled);

    /// <summary>
    /// The one query shape every read here goes through, joined to the priority for its
    /// code and rank.
    /// </summary>
    /// <remarks>
    /// A join rather than a correlated subquery, and an inner one because the foreign key
    /// is <c>NOT NULL</c> with <c>ON DELETE RESTRICT</c> — a ticket whose priority is
    /// missing cannot exist. It stops at <see cref="TicketRow"/> rather than building the
    /// summary outright because the status is stored through a value converter: rendering
    /// it as the string the contract carries is <see cref="Summarize"/>'s job, in memory,
    /// where <c>ToString</c> means what it says.
    /// </remarks>
    /// <param name="tickets">The ticket query, already narrowed, ordered, and paged.</param>
    /// <returns>The projected query. Nothing has been executed.</returns>
    private IQueryable<TicketRow> Project(IQueryable<Ticket> tickets) =>
        from ticket in tickets
        join priority in database.TicketPriorities on ticket.PriorityId equals priority.Id
        select new TicketRow(
            ticket.Id,
            ticket.Number,
            ticket.Subject,
            ticket.Status,
            priority.Code,
            priority.Rank,
            ticket.RequesterId,
            ticket.AssigneeId,
            ticket.RelatedAssetId,
            ticket.CreatedAt,
            ticket.DueAt,
            ticket.ResolvedAt,
            ticket.ClosedAt);

    /// <summary>Renders one projected row as the contract's shape.</summary>
    /// <param name="row">The row read from the database.</param>
    private static TicketSummary Summarize(TicketRow row) =>
        new(
            row.Id,
            row.Number,
            row.Subject,
            row.Status.ToString(),
            row.PriorityCode,
            row.PriorityRank,
            row.RequesterId,
            row.AssigneeId,
            row.RelatedAssetId,
            IsOpen(row.Status),
            row.CreatedAt,
            row.DueAt,
            row.ResolvedAt,
            row.ClosedAt);

    /// <summary>
    /// One ticket as the database hands it back, with the status still an enum.
    /// </summary>
    /// <remarks>Private: it exists only to get between the query and <see cref="Summarize"/>.</remarks>
    private sealed record TicketRow(
        Guid Id,
        string Number,
        string Subject,
        TicketStatus Status,
        string PriorityCode,
        int PriorityRank,
        Guid RequesterId,
        Guid? AssigneeId,
        Guid? RelatedAssetId,
        DateTimeOffset CreatedAt,
        DateTimeOffset DueAt,
        DateTimeOffset? ResolvedAt,
        DateTimeOffset? ClosedAt);
}
