using Itms.Modules.Helpdesk.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// The hold reason: required to park a ticket, cleared on resuming, and recorded in the
/// timeline both times.
/// </summary>
/// <remarks>
/// The rules mirror resolution notes exactly — required for one destination, refused for
/// every other — with one difference that matters: a resolution is <em>kept</em> through a
/// reopen because it is what the requester rejected, while a hold reason is <em>cleared</em>
/// on resuming because it describes a state the ticket is no longer in. The clearing is
/// also what makes a second hold produce a timeline entry at all.
/// </remarks>
public sealed class TicketHoldReasonTests
{
    private const string HoldReason = "Waiting on the vendor.";
    private static readonly Guid Technician = Guid.CreateVersion7();
    private static readonly SlaTargets Medium = new(30, 480);

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Holding_a_ticket_records_why()
    {
        var ticket = InProgress();

        var result = ticket.Wait(HoldReason, _clock.UtcNow, Technician);

        result.IsSuccess.ShouldBeTrue();
        ticket.Status.ShouldBe(TicketStatus.Waiting);
        ticket.HoldReason.ShouldBe(HoldReason);
    }

    [Fact]
    public void Holding_without_a_reason_is_refused()
    {
        var ticket = InProgress();

        var result = ticket.ChangeStatus(
            TicketStatus.Waiting, resolutionNotes: null, holdReason: null, _clock.UtcNow, Technician);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.hold_reason_required");
        ticket.Status.ShouldBe(TicketStatus.InProgress);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_reason_is_no_reason(string reason)
    {
        var ticket = InProgress();

        var result = ticket.Wait(reason, _clock.UtcNow, Technician);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.hold_reason_required");
    }

    [Fact]
    public void A_reason_longer_than_the_column_is_refused_rather_than_truncated()
    {
        var ticket = InProgress();

        var result = ticket.Wait(new string('x', Ticket.HoldReasonMaxLength + 1), _clock.UtcNow, Technician);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.hold_reason_too_long");
    }

    [Fact]
    public void The_reason_is_trimmed()
    {
        var ticket = InProgress();

        ticket.Wait("  Waiting on the vendor.  ", _clock.UtcNow, Technician);

        ticket.HoldReason.ShouldBe(HoldReason);
    }

    [Fact]
    public void A_reason_offered_when_cancelling_is_refused()
    {
        // The mirror of resolution notes: silently dropping text somebody typed is worse
        // than refusing it.
        var ticket = InProgress();

        var result = ticket.ChangeStatus(
            TicketStatus.Cancelled, resolutionNotes: null, holdReason: "Not a hold.", _clock.UtcNow, Technician);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.hold_reason_not_accepted");
        ticket.Status.ShouldBe(TicketStatus.InProgress);
    }

    [Fact]
    public void A_reason_offered_when_starting_work_is_refused()
    {
        var ticket = Assigned();

        var result = ticket.ChangeStatus(
            TicketStatus.InProgress, resolutionNotes: null, holdReason: "Not a hold.", _clock.UtcNow, Technician);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.hold_reason_not_accepted");
        ticket.Status.ShouldBe(TicketStatus.Assigned);
    }

    [Fact]
    public void A_reason_offered_alongside_a_valid_resolution_is_still_refused()
    {
        // Both fields present and only one of them belongs to this destination. The
        // resolution passes its check and the hold reason is what stops the move.
        var ticket = InProgress();

        var result = ticket.ChangeStatus(
            TicketStatus.Resolved, "Replaced the roller.", holdReason: "Not a hold.", _clock.UtcNow, Technician);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.hold_reason_not_accepted");
        ticket.Status.ShouldBe(TicketStatus.InProgress);
        ticket.ResolutionNotes.ShouldBeNull();
    }

    [Fact]
    public void Resuming_clears_the_reason()
    {
        var ticket = InProgress();
        ticket.Wait(HoldReason, _clock.UtcNow, Technician);

        ticket.Resume(_clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();

        ticket.HoldReason.ShouldBeNull();
    }

    [Fact]
    public void Cancelling_from_a_hold_clears_it_too()
    {
        // Leaving Waiting for anywhere at all: the ticket is not waiting on anything now.
        var ticket = InProgress();
        ticket.Wait(HoldReason, _clock.UtcNow, Technician);

        ticket.Cancel(_clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();

        ticket.HoldReason.ShouldBeNull();
    }

    [Fact]
    public void Holding_twice_for_the_same_reason_still_records_the_second_hold()
    {
        // This is what the clearing buys. If the reason survived the resume, the second
        // hold would produce no snapshot diff and therefore no timeline entry, and the
        // ticket's history would claim it was only ever held once.
        var ticket = InProgress();

        ticket.Wait(HoldReason, _clock.UtcNow, Technician);
        var beforeResume = TicketSnapshot.Of(ticket);
        ticket.Resume(_clock.UtcNow, Technician);
        var afterResume = TicketSnapshot.Of(ticket);
        ticket.Wait(HoldReason, _clock.UtcNow, Technician);
        var afterSecondHold = TicketSnapshot.Of(ticket);

        TicketChanges.Between(beforeResume, afterResume)
            .ShouldContain(change => change.Kind == TicketChangeKind.Hold && change.To == null);
        TicketChanges.Between(afterResume, afterSecondHold)
            .ShouldContain(change => change.Kind == TicketChangeKind.Hold && change.To == HoldReason);
    }

    [Fact]
    public void A_hold_writes_the_status_move_and_the_reason_as_two_lines_of_one_change()
    {
        // Exactly as resolving does, so a screen grouping entries by instant renders "on
        // hold, because X" as one event.
        var ticket = InProgress();
        var before = TicketSnapshot.Of(ticket);

        ticket.Wait(HoldReason, _clock.UtcNow, Technician);

        var changes = TicketChanges.Between(before, TicketSnapshot.Of(ticket));

        changes.Select(change => change.Kind)
            .ShouldBe([TicketChangeKind.Status, TicketChangeKind.Hold]);
        changes.Single(change => change.Kind == TicketChangeKind.Hold).To.ShouldBe(HoldReason);
    }

    private Ticket Assigned()
    {
        var ticket = Ticket.Create(
            "TKT-0042",
            new NewTicket(
                "Printer jammed",
                "It keeps jamming.",
                Guid.CreateVersion7(),
                "Jane Doe",
                Guid.CreateVersion7(),
                "Information Technology",
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Medium),
            _clock.UtcNow,
            Technician);

        ticket.Assign(Technician, "Toni Technician", _clock.UtcNow, Technician);
        return ticket;
    }

    private Ticket InProgress()
    {
        var ticket = Assigned();
        ticket.Start(_clock.UtcNow, Technician);
        return ticket;
    }
}
