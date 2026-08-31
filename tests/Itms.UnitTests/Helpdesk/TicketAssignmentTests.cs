using Itms.Modules.Helpdesk.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// <see cref="Ticket.Assign"/> and <see cref="Ticket.Unassign"/> — the entity half of
/// WP-1.6.
/// </summary>
/// <remarks>
/// <para>
/// The rule these exist to make true is that <b>a ticket's status and its assignee never
/// disagree</b>: Assigned always has somebody, New never does. That is why both writes
/// happen inside one entity method rather than being left to a handler to remember, and
/// it is why every assertion here goes through the entity rather than through a handler.
/// </para>
/// <para>
/// Who may be assigned — active, and holding Technician or Admin — is a fact about
/// Identity's rows that this entity cannot read. <c>TicketAssignmentEndpointTests</c>
/// covers it against the real lookup.
/// </para>
/// </remarks>
public sealed class TicketAssignmentTests
{
    private static readonly Guid Author = Guid.CreateVersion7();
    private static readonly Guid Priya = Guid.CreateVersion7();
    private static readonly Guid Sam = Guid.CreateVersion7();

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// The first assignment is what starts the workflow: the ticket leaves New and gains
    /// an owner in one call.
    /// </summary>
    [Fact]
    public void Assigning_a_new_ticket_moves_it_to_Assigned_and_records_the_technician()
    {
        var ticket = NewTicket();
        _clock.Advance(TimeSpan.FromMinutes(20));

        ticket.Assign(Priya, "Priya Raman", _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.Status.ShouldBe(TicketStatus.Assigned);
        ticket.AssigneeId.ShouldBe(Priya);
        ticket.AssigneeName.ShouldBe("Priya Raman");
        ticket.UpdatedAt.ShouldBe(_clock.UtcNow);
        ticket.UpdatedBy.ShouldBe(Author);
    }

    /// <summary>
    /// WP-1.6's own criterion: reassigning an in-progress ticket preserves its status.
    /// Handing work on does not restart it.
    /// </summary>
    [Theory]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Waiting)]
    [InlineData(TicketStatus.Resolved)]
    public void Reassignment_changes_the_technician_and_leaves_the_status_alone(TicketStatus status)
    {
        var ticket = TicketIn(status);
        _clock.Advance(TimeSpan.FromHours(1));

        ticket.Assign(Sam, "Sam Okonkwo", _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.Status.ShouldBe(status);
        ticket.AssigneeId.ShouldBe(Sam);
        ticket.AssigneeName.ShouldBe("Sam Okonkwo");
    }

    /// <summary>A Closed or Cancelled ticket has no work left to hand anybody.</summary>
    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void A_terminal_ticket_cannot_be_assigned(TicketStatus terminal)
    {
        var ticket = TicketIn(terminal);
        var holderBefore = ticket.AssigneeId;

        var result = ticket.Assign(Sam, "Sam Okonkwo", _clock.UtcNow, Author);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.ticket_not_assignable");
        ticket.Status.ShouldBe(terminal);
        ticket.AssigneeId.ShouldBe(holderBefore);
    }

    /// <summary>
    /// Assigning a ticket to whoever already holds it is refused rather than treated as a
    /// no-op — the same call WP-1.3 made for a move to the status a ticket is already in,
    /// and for the same reason: it would write a history line saying the ticket passed
    /// from somebody to themselves.
    /// </summary>
    [Fact]
    public void Assigning_the_current_holder_again_is_refused()
    {
        var ticket = TicketIn(TicketStatus.InProgress);
        var stampBefore = ticket.UpdatedAt;
        _clock.Advance(TimeSpan.FromHours(1));

        var result = ticket.Assign(Priya, "Priya Raman", _clock.UtcNow, Author);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.already_assigned");
        ticket.UpdatedAt.ShouldBe(stampBefore);
    }

    /// <summary>
    /// The same person under a new display name is still the same person, so this is a
    /// refusal too — the assignee is compared by id, exactly as
    /// <see cref="TicketChanges.Between"/> compares it.
    /// </summary>
    [Fact]
    public void A_renamed_technician_is_still_the_current_holder()
    {
        var ticket = TicketIn(TicketStatus.Assigned);

        var result = ticket.Assign(Priya, "Priya Raman-Silva", _clock.UtcNow, Author);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.already_assigned");
        ticket.AssigneeName.ShouldBe("Priya Raman");
    }

    /// <summary>Unassigning returns the ticket to New and clears the holder, in one call.</summary>
    [Fact]
    public void Unassigning_an_Assigned_ticket_returns_it_to_New_and_clears_the_holder()
    {
        var ticket = TicketIn(TicketStatus.Assigned);
        _clock.Advance(TimeSpan.FromHours(2));

        ticket.Unassign(_clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.Status.ShouldBe(TicketStatus.New);
        ticket.AssigneeId.ShouldBeNull();
        ticket.AssigneeName.ShouldBeNull();
        ticket.UpdatedAt.ShouldBe(_clock.UtcNow);
        ticket.UpdatedBy.ShouldBe(Author);
    }

    /// <summary>
    /// Once work has started the ticket has a history somebody owns. The answer to "this
    /// is not mine" is to hand it on, not to drop it back on the queue as though nothing
    /// had happened.
    /// </summary>
    [Theory]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Waiting)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void A_ticket_past_Assigned_cannot_be_unassigned(TicketStatus status)
    {
        var ticket = TicketIn(status);

        var result = ticket.Unassign(_clock.UtcNow, Author);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.cannot_unassign");
        ticket.Status.ShouldBe(status);
        ticket.AssigneeId.ShouldBe(Priya);
    }

    /// <summary>Nobody holds a New ticket, so there is nothing to take off them.</summary>
    [Fact]
    public void An_unassigned_ticket_cannot_be_unassigned()
    {
        var ticket = NewTicket();

        var result = ticket.Unassign(_clock.UtcNow, Author);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.ticket_not_assigned");
        ticket.Status.ShouldBe(TicketStatus.New);
    }

    /// <summary>
    /// The one route to a terminal ticket nobody ever held: cancelling straight from New,
    /// which SPEC.md §2 allows and which is not an unassignment.
    /// </summary>
    [Fact]
    public void A_ticket_cancelled_before_it_was_ever_assigned_holds_nobody()
    {
        var ticket = NewTicket();

        ticket.Cancel(_clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.Status.ShouldBe(TicketStatus.Cancelled);
        ticket.AssigneeId.ShouldBeNull();
        ticket.Unassign(_clock.UtcNow, Author).Error!.Code.ShouldBe("helpdesk.ticket_not_assigned");
    }

    /// <summary>
    /// The invariant the whole package exists to protect, walked over the states a ticket
    /// can actually be in: <b>Assigned has a holder, New does not.</b>
    /// </summary>
    [Theory]
    [InlineData(TicketStatus.New, false)]
    [InlineData(TicketStatus.Assigned, true)]
    [InlineData(TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.Waiting, true)]
    [InlineData(TicketStatus.Resolved, true)]
    [InlineData(TicketStatus.Closed, true)]
    public void A_ticket_holds_an_assignee_exactly_when_it_has_left_New(TicketStatus status, bool held)
    {
        var ticket = TicketIn(status);

        (ticket.AssigneeId is not null).ShouldBe(held);
    }

    /// <summary>
    /// Assignment round-trips: a ticket assigned and then unassigned is back where it
    /// started, and can be assigned again.
    /// </summary>
    [Fact]
    public void An_unassigned_ticket_can_be_picked_up_again()
    {
        var ticket = TicketIn(TicketStatus.Assigned);

        ticket.Unassign(_clock.UtcNow, Author).IsSuccess.ShouldBeTrue();
        ticket.Assign(Sam, "Sam Okonkwo", _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.Status.ShouldBe(TicketStatus.Assigned);
        ticket.AssigneeId.ShouldBe(Sam);
    }

    /// <summary>
    /// An empty id or a blank name is a caller inside the module building a request from
    /// unvalidated input, which CONVENTIONS.md says is what exceptions are for.
    /// </summary>
    [Fact]
    public void An_empty_assignee_id_throws_rather_than_being_recorded()
    {
        var ticket = NewTicket();

        Should.Throw<ArgumentException>(() => ticket.Assign(Guid.Empty, "Priya Raman", _clock.UtcNow, Author));
        ticket.Status.ShouldBe(TicketStatus.New);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_assignee_name_throws_rather_than_being_recorded(string name)
    {
        var ticket = NewTicket();

        Should.Throw<ArgumentException>(() => ticket.Assign(Priya, name, _clock.UtcNow, Author));
        ticket.AssigneeId.ShouldBeNull();
    }

    /// <summary>
    /// The name is trimmed the way every other piece of reference text on the entity is.
    /// </summary>
    [Fact]
    public void The_cached_display_name_is_trimmed()
    {
        var ticket = NewTicket();

        ticket.Assign(Priya, "  Priya Raman  ", _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.AssigneeName.ShouldBe("Priya Raman");
    }

    /// <summary>
    /// The history the recorder will write, read straight off the two snapshots. WP-1.4
    /// built <see cref="TicketChanges.Between"/> so that WP-1.6 would get its timeline
    /// without naming its own entries; this is that promise, asserted.
    /// </summary>
    [Fact]
    public void A_first_assignment_owes_a_status_line_and_an_assignment_line()
    {
        var ticket = NewTicket();
        var before = TicketSnapshot.Of(ticket);

        ticket.Assign(Priya, "Priya Raman", _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        var changes = TicketChanges.Between(before, TicketSnapshot.Of(ticket));

        changes.Count.ShouldBe(2);
        changes[0].ShouldBe(new TicketChange(TicketChangeKind.Status, "New", "Assigned"));
        changes[1].ShouldBe(new TicketChange(TicketChangeKind.Assignment, null, "Priya Raman"));
    }

    /// <summary>A reassignment moved nobody's status, so it owes one line, not two.</summary>
    [Fact]
    public void A_reassignment_owes_only_an_assignment_line()
    {
        var ticket = TicketIn(TicketStatus.InProgress);
        var before = TicketSnapshot.Of(ticket);

        ticket.Assign(Sam, "Sam Okonkwo", _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        var changes = TicketChanges.Between(before, TicketSnapshot.Of(ticket));

        changes.ShouldHaveSingleItem()
            .ShouldBe(new TicketChange(TicketChangeKind.Assignment, "Priya Raman", "Sam Okonkwo"));
    }

    /// <summary>Unassignment owes both lines too, and names who lost the ticket.</summary>
    [Fact]
    public void An_unassignment_owes_a_status_line_and_an_assignment_line()
    {
        var ticket = TicketIn(TicketStatus.Assigned);
        var before = TicketSnapshot.Of(ticket);

        ticket.Unassign(_clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        var changes = TicketChanges.Between(before, TicketSnapshot.Of(ticket));

        changes.Count.ShouldBe(2);
        changes[0].ShouldBe(new TicketChange(TicketChangeKind.Status, "Assigned", "New"));
        changes[1].ShouldBe(new TicketChange(TicketChangeKind.Assignment, "Priya Raman", null));
    }

    /// <summary>A refused assignment owes nothing: no line, and no stamp.</summary>
    [Fact]
    public void A_refused_assignment_owes_no_history()
    {
        var ticket = TicketIn(TicketStatus.Closed);
        var before = TicketSnapshot.Of(ticket);

        ticket.Assign(Sam, "Sam Okonkwo", _clock.UtcNow, Author).IsFailure.ShouldBeTrue();

        TicketChanges.Between(before, TicketSnapshot.Of(ticket)).ShouldBeEmpty();
    }

    /// <summary>
    /// A ticket in <paramref name="status"/>, walked there through the real methods only —
    /// <see cref="Ticket.Assign"/> included, which is the door WP-1.3's suite did not yet
    /// have.
    /// </summary>
    private Ticket TicketIn(TicketStatus status)
    {
        var ticket = NewTicket();

        if (status == TicketStatus.New)
        {
            return ticket;
        }

        ticket.Assign(Priya, "Priya Raman", _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        switch (status)
        {
            case TicketStatus.Assigned:
                return ticket;

            case TicketStatus.Cancelled:
                // Cancelled by way of Assigned, so it carries a holder like every other
                // state past New. Cancelling straight from New is the one route to a
                // terminal ticket nobody ever held — see
                // A_ticket_cancelled_before_it_was_ever_assigned_holds_nobody.
                ticket.Cancel(_clock.UtcNow, Author).IsSuccess.ShouldBeTrue();
                return ticket;

            case TicketStatus.Waiting:
                ticket.Wait(_clock.UtcNow, Author).IsSuccess.ShouldBeTrue();
                return ticket;

            case TicketStatus.InProgress:
                ticket.Start(_clock.UtcNow, Author).IsSuccess.ShouldBeTrue();
                return ticket;

            case TicketStatus.Resolved:
            case TicketStatus.Closed:
                ticket.Start(_clock.UtcNow, Author).IsSuccess.ShouldBeTrue();
                ticket.Resolve("Replaced the charger.", _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

                if (status == TicketStatus.Closed)
                {
                    ticket.Close(_clock.UtcNow, Author).IsSuccess.ShouldBeTrue();
                }

                return ticket;

            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "No walk to that state.");
        }
    }

    private Ticket NewTicket() => Ticket.Create(
        "TKT-0042",
        new NewTicket(
            "Laptop will not charge",
            "It stops at 40% and the light goes amber.",
            Guid.CreateVersion7(),
            "Dana Reyes",
            Guid.CreateVersion7(),
            "Water Operations",
            Guid.CreateVersion7(),
            Guid.CreateVersion7()),
        _clock.UtcNow,
        Author);
}
