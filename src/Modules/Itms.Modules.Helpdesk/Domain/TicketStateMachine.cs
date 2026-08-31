using System.Collections.Frozen;

namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// Which move between <see cref="TicketStatus"/> values is legal, written once.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a table rather than a chain of <c>if</c>s.</b> Invariant 2 says illegal
/// transitions are rejected server-side, and ARCHITECTURE.md is emphatic that hiding a
/// move in the UI is never the enforcement. A table can be enumerated, so the unit suite
/// asserts all forty-nine ordered pairs rather than the handful somebody remembered to
/// write a test for — which is the only way "every legal and illegal transition is
/// unit-tested" (WP-1.3) means anything.
/// </para>
/// <para>
/// <b>The table is deliberately strict.</b> It is exactly SPEC.md §2's chain —
/// <c>New → Assigned → In Progress → Waiting → Resolved → Closed</c> — plus the three
/// exceptions the spec names: <see cref="TicketStatus.Cancelled"/> from any
/// pre-<see cref="TicketStatus.Resolved"/> state, reopen from
/// <see cref="TicketStatus.Resolved"/> to <see cref="TicketStatus.InProgress"/>, and
/// <see cref="TicketStatus.Closed"/> as terminal. Shortcuts the spec does not draw —
/// <c>New → In Progress</c>, <c>Assigned → Resolved</c> — are absent at the human's
/// direction: an extra transition is cheap and a predictable workflow is not.
/// <see cref="TicketStatus.Cancelled"/> is terminal for the same reason Closed is:
/// SPEC.md names no way out of it, and terminal is the safer reading of silence.
/// </para>
/// <para>
/// <b>This type decides nothing about who may make a move, or what else the move
/// writes.</b> It is a pure lookup over the states. <see cref="Ticket"/> owns the field
/// writes and the invariants that go with them; the endpoint owns the policy, and owns
/// the separate question of which destinations <em>it</em> is able to offer.
/// </para>
/// </remarks>
public static class TicketStateMachine
{
    /// <summary>
    /// Every legal destination, keyed by origin. Frozen because it is read on every
    /// transition and never rebuilt.
    /// </summary>
    /// <remarks>
    /// <see cref="TicketStatus.Assigned"/> is in the table like any other state, because
    /// the table is the workflow and the workflow contains it. What it does <em>not</em>
    /// have is an intent-named wrapper on <see cref="Ticket"/>: a ticket reaches Assigned
    /// by being assigned to somebody, which is WP-1.6's <c>Assign</c>, and the
    /// status-change endpoint refuses that destination because it carries no assignee to
    /// go with it.
    /// </remarks>
    private static readonly FrozenDictionary<TicketStatus, FrozenSet<TicketStatus>> LegalDestinations =
        new Dictionary<TicketStatus, FrozenSet<TicketStatus>>
        {
            [TicketStatus.New] = Set(TicketStatus.Assigned, TicketStatus.Cancelled),
            [TicketStatus.Assigned] = Set(TicketStatus.InProgress, TicketStatus.Waiting, TicketStatus.Cancelled),
            [TicketStatus.InProgress] = Set(TicketStatus.Waiting, TicketStatus.Resolved, TicketStatus.Cancelled),
            [TicketStatus.Waiting] = Set(TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Cancelled),
            [TicketStatus.Resolved] = Set(TicketStatus.Closed, TicketStatus.InProgress),
            [TicketStatus.Closed] = FrozenSet<TicketStatus>.Empty,
            [TicketStatus.Cancelled] = FrozenSet<TicketStatus>.Empty,
        }.ToFrozenDictionary();

    /// <summary>
    /// Whether a ticket in <paramref name="from"/> may move to <paramref name="to"/>.
    /// </summary>
    /// <remarks>
    /// A move to the state the ticket is already in is <em>not</em> legal. It is not a
    /// no-op: it would write an audit row and, from WP-1.4, a history entry saying a
    /// ticket went from Waiting to Waiting. The caller asked for something that cannot
    /// happen, and 409 is the honest answer.
    /// </remarks>
    /// <param name="from">The status the ticket is in.</param>
    /// <param name="to">The status being asked for.</param>
    /// <returns><see langword="true"/> when the move is one SPEC.md §2 allows.</returns>
    public static bool CanTransition(TicketStatus from, TicketStatus to) =>
        LegalDestinations.TryGetValue(from, out var destinations) && destinations.Contains(to);

    /// <summary>Whether a ticket in <paramref name="status"/> can move anywhere at all.</summary>
    /// <param name="status">The status to ask about.</param>
    /// <returns><see langword="true"/> for <see cref="TicketStatus.Closed"/> and <see cref="TicketStatus.Cancelled"/>.</returns>
    public static bool IsTerminal(TicketStatus status) =>
        LegalDestinations.TryGetValue(status, out var destinations) && destinations.Count == 0;

    /// <summary>Every destination legal from <paramref name="from"/>, for a caller that wants to offer them.</summary>
    /// <remarks>
    /// WP-1.10 renders the transition buttons and must not render an illegal one. It
    /// should read this rather than restate the table in TypeScript.
    /// </remarks>
    /// <param name="from">The status the ticket is in.</param>
    /// <returns>The legal destinations, empty from a terminal state.</returns>
    public static IReadOnlyCollection<TicketStatus> DestinationsFrom(TicketStatus from) =>
        LegalDestinations.TryGetValue(from, out var destinations)
            ? destinations
            : FrozenSet<TicketStatus>.Empty;

    private static FrozenSet<TicketStatus> Set(params TicketStatus[] destinations) =>
        destinations.ToFrozenSet();
}
