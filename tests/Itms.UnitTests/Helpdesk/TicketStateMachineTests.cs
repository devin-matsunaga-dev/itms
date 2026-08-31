using Itms.Modules.Helpdesk.Domain;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// The transition table itself, asserted exhaustively.
/// </summary>
/// <remarks>
/// WP-1.3's done-criterion is "every legal and illegal transition is unit-tested". There
/// are seven states, so there are forty-nine ordered pairs, and this file asserts all
/// forty-nine — the legal ones by name and the rest by exclusion. A test that listed only
/// the transitions somebody thought of would leave the interesting ones uncovered, which
/// is the whole reason the machine is a table and not a chain of <c>if</c>s.
/// </remarks>
public sealed class TicketStateMachineTests
{
    /// <summary>
    /// SPEC.md §2's workflow, transcribed independently of
    /// <see cref="TicketStateMachine"/>'s own table.
    /// </summary>
    /// <remarks>
    /// Deliberately written out again rather than read from the implementation: a test
    /// that asks the code what it does and then agrees with it proves nothing. Change one
    /// and this suite fails until somebody changes the other on purpose.
    /// </remarks>
    private static readonly (TicketStatus From, TicketStatus To)[] LegalTransitions =
    [
        // The chain.
        (TicketStatus.New, TicketStatus.Assigned),
        (TicketStatus.Assigned, TicketStatus.InProgress),
        (TicketStatus.InProgress, TicketStatus.Waiting),
        (TicketStatus.Waiting, TicketStatus.InProgress),
        (TicketStatus.InProgress, TicketStatus.Resolved),
        (TicketStatus.Resolved, TicketStatus.Closed),

        // A ticket can be parked straight after it is picked up, and unparked to resolve.
        (TicketStatus.Assigned, TicketStatus.Waiting),
        (TicketStatus.Waiting, TicketStatus.Resolved),

        // Reopen.
        (TicketStatus.Resolved, TicketStatus.InProgress),

        // Cancelled from any pre-Resolved state.
        (TicketStatus.New, TicketStatus.Cancelled),
        (TicketStatus.Assigned, TicketStatus.Cancelled),
        (TicketStatus.InProgress, TicketStatus.Cancelled),
        (TicketStatus.Waiting, TicketStatus.Cancelled),
    ];

    public static TheoryData<TicketStatus, TicketStatus, bool> AllOrderedPairs()
    {
        var data = new TheoryData<TicketStatus, TicketStatus, bool>();

        foreach (var from in Enum.GetValues<TicketStatus>())
        {
            foreach (var to in Enum.GetValues<TicketStatus>())
            {
                data.Add(from, to, LegalTransitions.Contains((from, to)));
            }
        }

        return data;
    }

    /// <summary>The whole table, one assertion per ordered pair.</summary>
    [Theory]
    [MemberData(nameof(AllOrderedPairs))]
    public void Every_ordered_pair_of_states_is_legal_exactly_when_SPEC_2_says_so(
        TicketStatus from,
        TicketStatus to,
        bool expected) =>
        TicketStateMachine.CanTransition(from, to).ShouldBe(expected);

    /// <summary>Forty-nine pairs, and the guard that keeps that number honest.</summary>
    [Fact]
    public void There_are_seven_states_and_the_suite_covers_every_pair()
    {
        Enum.GetValues<TicketStatus>().Length.ShouldBe(7);
        AllOrderedPairs().Count.ShouldBe(49);
    }

    /// <summary>A ticket never returns to New. Nothing in the table points at it.</summary>
    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Waiting)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void Nothing_returns_a_ticket_to_New(TicketStatus from) =>
        TicketStateMachine.CanTransition(from, TicketStatus.New).ShouldBeFalse();

    /// <summary>Staying put is not a transition — see the remarks on <c>CanTransition</c>.</summary>
    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Waiting)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void A_state_is_never_legal_as_its_own_destination(TicketStatus status) =>
        TicketStateMachine.CanTransition(status, status).ShouldBeFalse();

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void Closed_and_Cancelled_are_terminal(TicketStatus terminal)
    {
        TicketStateMachine.IsTerminal(terminal).ShouldBeTrue();
        TicketStateMachine.DestinationsFrom(terminal).ShouldBeEmpty();

        foreach (var to in Enum.GetValues<TicketStatus>())
        {
            TicketStateMachine.CanTransition(terminal, to).ShouldBeFalse();
        }
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Waiting)]
    [InlineData(TicketStatus.Resolved)]
    public void Every_other_state_can_still_move(TicketStatus status) =>
        TicketStateMachine.IsTerminal(status).ShouldBeFalse();

    /// <summary>
    /// Cancelling is available right up until the ticket is resolved, and not after —
    /// SPEC.md §2's "from any pre-Resolved state", asserted as the boundary it is.
    /// </summary>
    [Theory]
    [InlineData(TicketStatus.New, true)]
    [InlineData(TicketStatus.Assigned, true)]
    [InlineData(TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.Waiting, true)]
    [InlineData(TicketStatus.Resolved, false)]
    [InlineData(TicketStatus.Closed, false)]
    [InlineData(TicketStatus.Cancelled, false)]
    public void Cancellation_is_reachable_from_every_pre_resolved_state_and_no_other(
        TicketStatus from,
        bool expected) =>
        TicketStateMachine.CanTransition(from, TicketStatus.Cancelled).ShouldBe(expected);

    /// <summary>
    /// The shortcuts SPEC.md §2 does not draw, named so a later session cannot quietly
    /// add one and still see a green suite.
    /// </summary>
    [Theory]
    [InlineData(TicketStatus.New, TicketStatus.InProgress)]
    [InlineData(TicketStatus.New, TicketStatus.Waiting)]
    [InlineData(TicketStatus.New, TicketStatus.Resolved)]
    [InlineData(TicketStatus.New, TicketStatus.Closed)]
    [InlineData(TicketStatus.Assigned, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Assigned, TicketStatus.Closed)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Closed)]
    [InlineData(TicketStatus.Waiting, TicketStatus.Closed)]
    [InlineData(TicketStatus.Waiting, TicketStatus.Assigned)]
    public void The_shortcuts_the_spec_does_not_draw_stay_illegal(TicketStatus from, TicketStatus to) =>
        TicketStateMachine.CanTransition(from, to).ShouldBeFalse();

    [Fact]
    public void The_destinations_offered_are_exactly_the_ones_that_are_legal()
    {
        foreach (var from in Enum.GetValues<TicketStatus>())
        {
            var offered = TicketStateMachine.DestinationsFrom(from);

            foreach (var to in Enum.GetValues<TicketStatus>())
            {
                offered.Contains(to).ShouldBe(TicketStateMachine.CanTransition(from, to));
            }
        }
    }
}
