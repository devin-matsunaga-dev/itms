using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.Tickets.ListTickets;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.Tickets.TicketCounters;

/// <summary>
/// Counts the queue: the KPI row's four numbers, plus the totals beside the saved views.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every count runs through <see cref="ListTicketsHandler.Filter"/>.</b> Each one is
/// expressed as the <see cref="ListTicketsQuery"/> that produces the list it summarises,
/// so a tile and the screen it links to cannot disagree — the failure that a
/// hand-written second set of predicates would eventually produce, and that no test would
/// catch until somebody counted the rows by hand. `TicketCountersTests` asserts the
/// equality directly.
/// </para>
/// <para>
/// <b>Scope first, exactly as the list does.</b> <see cref="TicketScope.VisibleTo"/> is
/// applied once and every count is taken from inside it, so a User's numbers describe
/// only the tickets they raised. A count is as much a disclosure as a row: "there are 24
/// open tickets" is information about tickets they cannot read.
/// </para>
/// <para>
/// <b>Why there are no deltas.</b> The mockup asks each tile for a change against the
/// same hour seven days ago, and the schema cannot answer it honestly. A ticket's row
/// holds only its <em>current</em> state: reconstructing "was this open last Tuesday"
/// from <c>resolved_at</c> and <c>closed_at</c> is wrong in two directions — a reopen
/// clears <c>resolved_at</c> (WP-1.3), so a ticket resolved ten days ago and reopened
/// three days ago looks as though it was never resolved at all; and a cancellation
/// records no instant, because WP-1.3 deliberately added no <c>cancelled_at</c>. Neither
/// can an assignment be placed in time from the ticket row, so "unassigned last week" is
/// unanswerable too.
/// </para>
/// <para>
/// The honest routes are a replay of <c>ticket_history</c>'s Status entries — which does
/// carry every transition with its instant, at the cost of a correlated subquery per
/// ticket — or a nightly counters snapshot, which is the fifth thing wanting the
/// hosted-service pattern STATUS.md has been collecting since WP-0.4.
/// <c>WP-5.1 — Dashboard backend</c> needs "ticket counters with deltas" in exactly the
/// same shape and should build whichever of the two once, for both screens. Shipping a
/// number here that looked like a trend and was not would be worse than shipping none.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="currentUser">Who is asking. Decides how much of the queue exists.</param>
/// <param name="clock">The system clock, read once so every count describes one instant.</param>
internal sealed class TicketCountersHandler(
    HelpdeskDbContext database,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>
    /// The four statuses that mean a ticket is still being worked.
    /// </summary>
    /// <remarks>
    /// The complement of <c>TicketStateMachine.IsTerminal</c> plus Resolved, which is
    /// terminal for this purpose and not for the state machine's — a resolved ticket can
    /// still be reopened, but nobody is working it and it does not belong in an "open"
    /// count. The client repeats this set to build the tile's click-through link, and
    /// `TicketCountersTests` asserts the two agree.
    /// </remarks>
    private static readonly TicketStatus[] OpenStatuses =
    [
        TicketStatus.New,
        TicketStatus.Assigned,
        TicketStatus.InProgress,
        TicketStatus.Waiting,
    ];

    /// <summary>Counts the queue.</summary>
    /// <param name="query">Carries the caller's own end of day.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The counters. Never a failure: an empty queue is six zeroes.</returns>
    public async Task<Result<TicketCountersResponse>> HandleAsync(
        TicketCountersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Read once, so six counts describe one instant. Two of them compare against it,
        // and a ticket that breached between the first and the last would otherwise be
        // both overdue and on track in the same response.
        var now = clock.UtcNow;
        var dueBefore = query.DueBefore ?? EndOfUtcDay(now);

        var scoped = database.Tickets.AsNoTracking().VisibleTo(currentUser);

        var mine = TicketScope.SeesEveryTicket(currentUser)
            ? new ListTicketsQuery { AssigneeId = currentUser.UserId }
            : new ListTicketsQuery { RequesterId = currentUser.UserId };

        return new TicketCountersResponse(
            All: await Count(scoped, new ListTicketsQuery(), now, cancellationToken).ConfigureAwait(false),
            Open: await Count(scoped, new ListTicketsQuery { Status = OpenStatuses }, now, cancellationToken).ConfigureAwait(false),
            Unassigned: await Count(scoped, new ListTicketsQuery { Status = OpenStatuses, Unassigned = true }, now, cancellationToken).ConfigureAwait(false),
            Overdue: await Count(scoped, new ListTicketsQuery { SlaState = Domain.SlaState.Breached }, now, cancellationToken).ConfigureAwait(false),
            DueToday: await Count(scoped, new ListTicketsQuery { DueBefore = dueBefore }, now, cancellationToken).ConfigureAwait(false),
            Mine: await Count(scoped, mine, now, cancellationToken).ConfigureAwait(false));
    }

    private static Task<int> Count(
        IQueryable<Ticket> scoped,
        ListTicketsQuery filters,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ListTicketsHandler.Filter(scoped, filters, now).CountAsync(cancellationToken);

    /// <summary>
    /// The last instant of the server's own UTC day — the fallback when a caller named no
    /// day of its own.
    /// </summary>
    private static DateTimeOffset EndOfUtcDay(DateTimeOffset now) =>
        new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(1);
}
