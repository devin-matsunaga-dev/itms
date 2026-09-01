namespace Itms.Contracts.Lookups;

/// <summary>
/// The fields another module is allowed to know about a ticket. Flat and small for the
/// same reason <see cref="AssetSummary"/> is: it is a display and reference projection,
/// and no module outside Helpdesk should be able to reason about the state machine,
/// the SLA arithmetic, or the conversation from it.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no description, no resolution, and no comment count.</b> A ticket body is
/// eight thousand characters and an internal note is the one thing a requester may never
/// read (ARCHITECTURE.md §7); neither belongs in a shape that leaves the module. What
/// travels is what a panel on somebody else's screen renders — the number, the subject,
/// where it sits, and when it is due.
/// </para>
/// <para>
/// <b><see cref="IsOpen"/> is carried rather than derived.</b> Which statuses count as
/// open is Helpdesk's own business, and a consumer that decided for itself would be a
/// second copy of the state machine's terminal set — which is exactly the drift
/// <c>AllowedNextStatuses</c> exists to prevent on the wire.
/// </para>
/// </remarks>
/// <param name="Id">The ticket's id.</param>
/// <param name="Number">The human-readable number, <c>TKT-####</c>. What people quote.</param>
/// <param name="Subject">The one-line summary.</param>
/// <param name="Status">Where it sits in the workflow, as a string — the enum stays inside Helpdesk.</param>
/// <param name="PriorityCode">The priority's stable key, for colour and for rules.</param>
/// <param name="PriorityRank">Its ordering weight, so a panel can sort without a second call.</param>
/// <param name="RequesterId">Who the ticket is for.</param>
/// <param name="AssigneeId">The technician responsible, or <see langword="null"/>.</param>
/// <param name="RelatedAssetId">The asset it concerns, or <see langword="null"/>.</param>
/// <param name="IsOpen">
/// False once the ticket is resolved, closed, or cancelled. The split SPEC.md §4 asks a
/// user page for — open tickets above, previous tickets below.
/// </param>
/// <param name="CreatedAt">When it was raised (UTC).</param>
/// <param name="DueAt">When resolution is due (UTC), pauses included.</param>
/// <param name="ResolvedAt">When it was resolved (UTC), or <see langword="null"/>.</param>
/// <param name="ClosedAt">When it was closed (UTC), or <see langword="null"/>.</param>
public sealed record TicketSummary(
    Guid Id,
    string Number,
    string Subject,
    string Status,
    string PriorityCode,
    int PriorityRank,
    Guid RequesterId,
    Guid? AssigneeId,
    Guid? RelatedAssetId,
    bool IsOpen,
    DateTimeOffset CreatedAt,
    DateTimeOffset DueAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt);

/// <summary>
/// One page of tickets, as a lookup answers it.
/// </summary>
/// <remarks>
/// The same four members as <c>Itms.Platform.Paging.PagedResult&lt;T&gt;</c> and
/// deliberately not that type: the contracts assembly references nothing in the solution
/// — <c>ModuleBoundaryTests.Contracts_references_nothing_in_the_solution</c> fails the
/// build if it ever does — because a contract that dragged the shared kernel along would
/// make the kernel a second contract surface. A consumer maps this onto the API envelope
/// in one line, which is a cheaper price than that.
/// </remarks>
/// <param name="Items">The tickets on this page.</param>
/// <param name="Total">How many match in total, across every page.</param>
/// <param name="Page">The 1-based page number this represents.</param>
/// <param name="PageSize">The page size that was applied.</param>
public sealed record TicketPage(
    IReadOnlyList<TicketSummary> Items,
    int Total,
    int Page,
    int PageSize);

/// <summary>
/// Which half of somebody's support history is being asked for.
/// </summary>
/// <remarks>
/// SPEC.md §4 wants a user page showing "assigned assets, open tickets, and previous
/// tickets" — two lists, not one list a caller filters. The enum is the question, and
/// Helpdesk answers it, so nothing outside the module has to know which statuses are
/// terminal.
/// </remarks>
public enum TicketActivity
{
    /// <summary>Every ticket, open or not, newest first.</summary>
    All,

    /// <summary>Only tickets still being worked: anything not resolved, closed, or cancelled.</summary>
    Open,

    /// <summary>Only tickets that are finished with: resolved, closed, or cancelled.</summary>
    Past,
}

/// <summary>
/// How every other module reads tickets. Assets needs an asset's support history and
/// Identity needs a person's; neither may reference <c>Modules.Helpdesk</c> or query its
/// tables (ARCHITECTURE.md §3 rules 1 and 2), so they take this instead.
/// </summary>
/// <remarks>
/// <b>Every read here is unscoped.</b> The row-level rule that a User sees only the
/// tickets they raised is <c>TicketScope</c>'s, and it guards Helpdesk's own endpoints; a
/// consumer of this interface is answering a question about a person or an asset it has
/// already decided the caller may ask about, and a second scope applied here would silently
/// empty a technician's panel. A caller that exposes one of these over HTTP owns the
/// authorization on that route.
/// </remarks>
public interface ITicketLookup
{
    /// <summary>The ticket with <paramref name="ticketId"/>, or <see langword="null"/> if it does not exist or is soft-deleted.</summary>
    /// <param name="ticketId">The ticket wanted.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<TicketSummary?> GetAsync(Guid ticketId, CancellationToken cancellationToken);

    /// <summary>
    /// The tickets in <paramref name="ticketIds"/> that exist. Batched so a screen
    /// resolving twenty ticket numbers issues one query, not twenty.
    /// </summary>
    /// <param name="ticketIds">The tickets wanted. Duplicates are ignored.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<TicketSummary>> GetManyAsync(IReadOnlyCollection<Guid> ticketIds, CancellationToken cancellationToken);

    /// <summary>
    /// A page of the tickets <paramref name="requesterId"/> raised, newest first — the
    /// support history half of a user page.
    /// </summary>
    /// <param name="requesterId">Whose tickets are wanted.</param>
    /// <param name="activity">Which half: open, past, or both.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">How many to a page. The implementation clamps it.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<TicketPage> GetForRequesterAsync(
        Guid requesterId,
        TicketActivity activity,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// A page of the tickets linked to <paramref name="assetId"/>, newest first — what an
    /// asset's detail screen shows as its support history.
    /// </summary>
    /// <param name="assetId">The asset whose tickets are wanted.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">How many to a page. The implementation clamps it.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<TicketPage> GetForAssetAsync(
        Guid assetId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
