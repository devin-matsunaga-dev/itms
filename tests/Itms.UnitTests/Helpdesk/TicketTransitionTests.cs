using Itms.Modules.Helpdesk.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// <see cref="Ticket.ChangeStatus"/> — the entity half of WP-1.3. The table is asserted in
/// <see cref="TicketStateMachineTests"/>; this asserts that the entity actually obeys it,
/// and what else each move writes.
/// </summary>
/// <remarks>
/// Invariant 2 says an illegal transition is rejected server-side. "Server-side" is
/// satisfied by the endpoint, but <em>enforced in the entity</em> is what WP-1.3 asks
/// for — so every assertion here goes through the entity, not through a handler.
/// </remarks>
public sealed class TicketTransitionTests
{
    private static readonly Guid Author = Guid.CreateVersion7();
    private static readonly Guid Technician = Guid.CreateVersion7();

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// A ticket parked in <paramref name="status"/>, walked there through legal moves only.
    /// </summary>
    /// <remarks>
    /// Deliberately not a back door that writes the field: if the only way to reach
    /// Waiting in a test were to set it directly, the test would never notice the day the
    /// path to Waiting broke. <see cref="TicketStatus.Assigned"/> is reached with the bare
    /// <see cref="Ticket.ChangeStatus"/> because WP-1.6's <c>Assign</c> does not exist yet.
    /// </remarks>
    private Ticket TicketIn(TicketStatus status)
    {
        var ticket = NewTicket();

        if (status == TicketStatus.New)
        {
            return ticket;
        }

        if (status == TicketStatus.Cancelled)
        {
            ticket.Cancel(_clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();
            return ticket;
        }

        ticket.ChangeStatus(TicketStatus.Assigned, null, _clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();

        switch (status)
        {
            case TicketStatus.Assigned:
                return ticket;

            case TicketStatus.Waiting:
                ticket.Wait(_clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();
                return ticket;

            case TicketStatus.InProgress:
                ticket.Start(_clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();
                return ticket;

            case TicketStatus.Resolved:
            case TicketStatus.Closed:
                ticket.Start(_clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();
                ticket.Resolve("Replaced the charger.", _clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();

                if (status == TicketStatus.Closed)
                {
                    ticket.Close(_clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();
                }

                return ticket;

            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "No walk to that state.");
        }
    }

    public static TheoryData<TicketStatus, TicketStatus> IllegalPairs()
    {
        var data = new TheoryData<TicketStatus, TicketStatus>();

        foreach (var from in Enum.GetValues<TicketStatus>())
        {
            foreach (var to in Enum.GetValues<TicketStatus>())
            {
                if (!TicketStateMachine.CanTransition(from, to))
                {
                    data.Add(from, to);
                }
            }
        }

        return data;
    }

    public static TheoryData<TicketStatus, TicketStatus> LegalPairs()
    {
        var data = new TheoryData<TicketStatus, TicketStatus>();

        foreach (var from in Enum.GetValues<TicketStatus>())
        {
            foreach (var to in TicketStateMachine.DestinationsFrom(from))
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    /// <summary>Every illegal pair is refused by the entity, and leaves it untouched.</summary>
    [Theory]
    [MemberData(nameof(IllegalPairs))]
    public void An_illegal_transition_is_refused_and_changes_nothing(TicketStatus from, TicketStatus to)
    {
        var ticket = TicketIn(from);
        var stampBefore = ticket.UpdatedAt;
        var actorBefore = ticket.UpdatedBy;
        var resolvedBefore = ticket.ResolvedAt;
        var closedBefore = ticket.ClosedAt;
        var notesBefore = ticket.ResolutionNotes;

        _clock.Advance(TimeSpan.FromHours(1));
        var result = ticket.ChangeStatus(to, to == TicketStatus.Resolved ? "Done." : null, _clock.UtcNow, Author);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.illegal_transition");
        ticket.Status.ShouldBe(from);
        ticket.UpdatedAt.ShouldBe(stampBefore);
        ticket.UpdatedBy.ShouldBe(actorBefore);
        ticket.ResolvedAt.ShouldBe(resolvedBefore);
        ticket.ClosedAt.ShouldBe(closedBefore);
        ticket.ResolutionNotes.ShouldBe(notesBefore);
    }

    /// <summary>Every legal pair is accepted by the entity and moves it.</summary>
    [Theory]
    [MemberData(nameof(LegalPairs))]
    public void A_legal_transition_moves_the_ticket_and_stamps_it(TicketStatus from, TicketStatus to)
    {
        var ticket = TicketIn(from);

        _clock.Advance(TimeSpan.FromHours(1));
        var result = ticket.ChangeStatus(to, to == TicketStatus.Resolved ? "Done." : null, _clock.UtcNow, Author);

        result.IsSuccess.ShouldBeTrue();
        ticket.Status.ShouldBe(to);
        ticket.UpdatedAt.ShouldBe(_clock.UtcNow);
        ticket.UpdatedBy.ShouldBe(Author);
    }

    [Fact]
    public void A_terminal_ticket_says_so_rather_than_naming_the_destination()
    {
        var closed = TicketIn(TicketStatus.Closed);

        var result = closed.ChangeStatus(TicketStatus.InProgress, null, _clock.UtcNow, Author);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Message.ShouldBe("A Closed ticket cannot change status.");
    }

    [Fact]
    public void Resolving_records_the_notes_and_the_instant()
    {
        var ticket = TicketIn(TicketStatus.InProgress);
        _clock.Advance(TimeSpan.FromHours(3));

        ticket.Resolve("Replaced the charger and tested for an hour.", _clock.UtcNow, Technician)
            .IsSuccess.ShouldBeTrue();

        ticket.Status.ShouldBe(TicketStatus.Resolved);
        ticket.ResolutionNotes.ShouldBe("Replaced the charger and tested for an hour.");
        ticket.ResolvedAt.ShouldBe(_clock.UtcNow);
        ticket.ClosedAt.ShouldBeNull();
    }

    [Fact]
    public void Resolution_notes_are_trimmed()
    {
        var ticket = TicketIn(TicketStatus.InProgress);

        ticket.Resolve("  Replaced the charger.\n", _clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();

        ticket.ResolutionNotes.ShouldBe("Replaced the charger.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Resolving_without_saying_what_was_done_is_refused(string notes)
    {
        var ticket = TicketIn(TicketStatus.InProgress);

        var result = ticket.ChangeStatus(TicketStatus.Resolved, notes, _clock.UtcNow, Technician);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.resolution_notes_required");
        result.Error.FieldErrors.ShouldNotBeNull().ShouldContainKey("resolutionNotes");
        ticket.Status.ShouldBe(TicketStatus.InProgress);
        ticket.ResolvedAt.ShouldBeNull();
    }

    [Fact]
    public void Resolving_with_a_null_note_is_refused()
    {
        var ticket = TicketIn(TicketStatus.InProgress);

        var result = ticket.ChangeStatus(TicketStatus.Resolved, resolutionNotes: null, _clock.UtcNow, Technician);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.resolution_notes_required");
    }

    [Fact]
    public void Resolution_notes_longer_than_the_column_are_refused()
    {
        var ticket = TicketIn(TicketStatus.InProgress);

        var result = ticket.ChangeStatus(
            TicketStatus.Resolved,
            new string('x', Ticket.ResolutionNotesMaxLength + 1),
            _clock.UtcNow,
            Technician);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.resolution_notes_too_long");
        ticket.Status.ShouldBe(TicketStatus.InProgress);
    }

    /// <summary>Notes on a transition that does not record one would be silently dropped.</summary>
    [Theory]
    [InlineData(TicketStatus.Waiting)]
    [InlineData(TicketStatus.Cancelled)]
    public void Resolution_notes_on_any_other_transition_are_refused(TicketStatus target)
    {
        var ticket = TicketIn(TicketStatus.InProgress);

        var result = ticket.ChangeStatus(target, "Not a resolution.", _clock.UtcNow, Technician);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.resolution_notes_not_accepted");
        ticket.Status.ShouldBe(TicketStatus.InProgress);
    }

    [Fact]
    public void Closing_records_the_instant_and_keeps_the_resolution()
    {
        var ticket = TicketIn(TicketStatus.Resolved);
        var resolvedAt = ticket.ResolvedAt;
        _clock.Advance(TimeSpan.FromDays(2));

        ticket.Close(_clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.Status.ShouldBe(TicketStatus.Closed);
        ticket.ClosedAt.ShouldBe(_clock.UtcNow);
        ticket.ResolvedAt.ShouldBe(resolvedAt);
        ticket.ResolutionNotes.ShouldBe("Replaced the charger.");
    }

    /// <summary>
    /// Reopening clears the instant, because the ticket is not resolved any more, and
    /// keeps the notes, because they are what the requester rejected.
    /// </summary>
    [Fact]
    public void Reopening_clears_the_resolved_instant_and_keeps_the_notes()
    {
        var ticket = TicketIn(TicketStatus.Resolved);
        _clock.Advance(TimeSpan.FromDays(1));

        ticket.Reopen(_clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.Status.ShouldBe(TicketStatus.InProgress);
        ticket.ResolvedAt.ShouldBeNull();
        ticket.ResolutionNotes.ShouldBe("Replaced the charger.");
        ticket.ClosedAt.ShouldBeNull();
    }

    [Fact]
    public void Re_resolving_a_reopened_ticket_overwrites_the_notes_and_the_instant()
    {
        var ticket = TicketIn(TicketStatus.Resolved);
        ticket.Reopen(_clock.UtcNow, Author).IsSuccess.ShouldBeTrue();
        _clock.Advance(TimeSpan.FromHours(6));

        ticket.Resolve("Replaced the mainboard; the charger was fine.", _clock.UtcNow, Technician)
            .IsSuccess.ShouldBeTrue();

        ticket.ResolutionNotes.ShouldBe("Replaced the mainboard; the charger was fine.");
        ticket.ResolvedAt.ShouldBe(_clock.UtcNow);
    }

    /// <summary>Resuming out of Waiting is not a reopen: there was nothing to clear.</summary>
    [Fact]
    public void Resuming_from_Waiting_touches_no_resolution_field()
    {
        var ticket = TicketIn(TicketStatus.Waiting);

        ticket.Resume(_clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();

        ticket.Status.ShouldBe(TicketStatus.InProgress);
        ticket.ResolvedAt.ShouldBeNull();
        ticket.ResolutionNotes.ShouldBeNull();
    }

    [Fact]
    public void Cancelling_writes_no_resolution_and_no_closure()
    {
        var ticket = TicketIn(TicketStatus.InProgress);

        ticket.Cancel(_clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.Status.ShouldBe(TicketStatus.Cancelled);
        ticket.ResolvedAt.ShouldBeNull();
        ticket.ClosedAt.ShouldBeNull();
        ticket.ResolutionNotes.ShouldBeNull();
    }

    /// <summary>Cancelled is terminal — the human's call, and the safer reading of SPEC.md's silence.</summary>
    [Fact]
    public void A_cancelled_ticket_cannot_be_revived()
    {
        var ticket = TicketIn(TicketStatus.Cancelled);

        foreach (var target in Enum.GetValues<TicketStatus>())
        {
            ticket.ChangeStatus(target, target == TicketStatus.Resolved ? "Done." : null, _clock.UtcNow, Author)
                .IsFailure.ShouldBeTrue();
        }

        ticket.Status.ShouldBe(TicketStatus.Cancelled);
    }

    /// <summary>Neither shortcut exists, asserted through the entity as well as the table.</summary>
    [Fact]
    public void A_new_ticket_cannot_be_started_and_an_assigned_one_cannot_be_resolved()
    {
        TicketIn(TicketStatus.New)
            .Start(_clock.UtcNow, Technician)
            .Error!.Code.ShouldBe("helpdesk.illegal_transition");

        TicketIn(TicketStatus.Assigned)
            .Resolve("Fixed it on the phone.", _clock.UtcNow, Technician)
            .Error!.Code.ShouldBe("helpdesk.illegal_transition");
    }

    /// <summary>Every instant written comes from the clock the caller passed, never from the wall.</summary>
    [Fact]
    public void Every_transition_takes_its_instant_from_the_clock()
    {
        var ticket = TicketIn(TicketStatus.InProgress);
        _clock.Advance(TimeSpan.FromDays(400));
        var expected = _clock.UtcNow;

        ticket.Resolve("Done.", expected, Technician).IsSuccess.ShouldBeTrue();
        ticket.Close(expected, Technician).IsSuccess.ShouldBeTrue();

        ticket.ResolvedAt.ShouldBe(expected);
        ticket.ClosedAt.ShouldBe(expected);
        ticket.UpdatedAt.ShouldBe(expected);
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
