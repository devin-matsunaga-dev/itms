using Itms.Modules.Helpdesk.Domain;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// <see cref="TicketChanges.Between"/> and <see cref="TicketHistoryEntry"/> — the pure
/// half of WP-1.4.
/// </summary>
/// <remarks>
/// <para>
/// Which entries a change owes is the decision the whole package turns on: WP-1.6's
/// assignment and whatever package first moves a priority both get their history from
/// here, so it is worth asserting exhaustively while it is still cheap. The other half —
/// that the entries reach the database in the same transaction as the change, and never
/// outlive one that was rolled back — is invariant 3 and is asserted against a real
/// database in the integration suite.
/// </para>
/// <para>
/// The snapshots are built by hand rather than read off a <see cref="Ticket"/>, because
/// there is no domain method that moves a priority or an assignee yet and a test that
/// could only exercise the two dimensions WP-1.3 already moves would leave the other two
/// unasserted until the package that needs them.
/// </para>
/// </remarks>
public sealed class TicketHistoryTests
{
    private static readonly Guid Critical = Guid.CreateVersion7();
    private static readonly Guid Medium = Guid.CreateVersion7();
    private static readonly Guid Dana = Guid.CreateVersion7();
    private static readonly Guid Sam = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 9, 0, 0, TimeSpan.Zero);

    private static TicketSnapshot Snapshot(
        TicketStatus status = TicketStatus.InProgress,
        Guid? priorityId = null,
        Guid? assigneeId = null,
        string? assigneeName = null,
        string? resolutionNotes = null,
        string? holdReason = null) =>
        new(status, priorityId ?? Medium, assigneeId, assigneeName, resolutionNotes, holdReason);

    /// <summary>A change that moved nothing tracked writes no line at all.</summary>
    /// <remarks>
    /// This is what keeps a handler that saves back every field from making the timeline
    /// read as a rewrite of the whole ticket — the rule ARCHITECTURE.md §8 sets for the
    /// audit diff, applied to the narrative.
    /// </remarks>
    [Fact]
    public void An_unchanged_ticket_owes_no_history()
    {
        var snapshot = Snapshot();

        TicketChanges.Between(snapshot, snapshot).ShouldBeEmpty();
    }

    /// <summary>A status move writes one line carrying both ends of it.</summary>
    [Fact]
    public void A_status_move_is_recorded_from_and_to()
    {
        var changes = TicketChanges.Between(
            Snapshot(status: TicketStatus.Waiting),
            Snapshot(status: TicketStatus.InProgress));

        var change = changes.ShouldHaveSingleItem();
        change.Kind.ShouldBe(TicketChangeKind.Status);
        change.From.ShouldBe("Waiting");
        change.To.ShouldBe("InProgress");
    }

    /// <summary>A priority move is described by the two names, not by the two ids.</summary>
    [Fact]
    public void A_priority_move_is_recorded_by_name()
    {
        var changes = TicketChanges.Between(
            Snapshot(priorityId: Medium),
            Snapshot(priorityId: Critical),
            new TicketPriorityNames("Medium", "Critical"));

        var change = changes.ShouldHaveSingleItem();
        change.Kind.ShouldBe(TicketChangeKind.Priority);
        change.From.ShouldBe("Medium");
        change.To.ShouldBe("Critical");
    }

    /// <summary>
    /// A priority move with no names to describe it throws rather than recording a line
    /// that says the priority changed without saying to what.
    /// </summary>
    /// <remarks>
    /// A programming error, not a caller's: the recorder resolves the names before it asks.
    /// Reaching it means a new call site forgot to, and an entry that looks like coverage
    /// and is not is worse than the exception.
    /// </remarks>
    [Fact]
    public void A_priority_move_without_names_is_a_programming_error()
    {
        Should.Throw<ArgumentException>(() => TicketChanges.Between(
            Snapshot(priorityId: Medium),
            Snapshot(priorityId: Critical)));
    }

    /// <summary>Assigning a ticket nobody held records the arrival, with no before.</summary>
    [Fact]
    public void An_assignment_from_nobody_has_no_from_value()
    {
        var changes = TicketChanges.Between(
            Snapshot(assigneeId: null, assigneeName: null),
            Snapshot(assigneeId: Dana, assigneeName: "Dana Reyes"));

        var change = changes.ShouldHaveSingleItem();
        change.Kind.ShouldBe(TicketChangeKind.Assignment);
        change.From.ShouldBeNull();
        change.To.ShouldBe("Dana Reyes");
    }

    /// <summary>Unassigning records the departure, with no after.</summary>
    [Fact]
    public void An_unassignment_has_no_to_value()
    {
        var changes = TicketChanges.Between(
            Snapshot(assigneeId: Dana, assigneeName: "Dana Reyes"),
            Snapshot(assigneeId: null, assigneeName: null));

        var change = changes.ShouldHaveSingleItem();
        change.Kind.ShouldBe(TicketChangeKind.Assignment);
        change.From.ShouldBe("Dana Reyes");
        change.To.ShouldBeNull();
    }

    /// <summary>
    /// Two technicians sharing a display name still produce a reassignment, because the
    /// comparison is on the id.
    /// </summary>
    [Fact]
    public void A_reassignment_between_namesakes_is_still_a_reassignment()
    {
        var changes = TicketChanges.Between(
            Snapshot(assigneeId: Dana, assigneeName: "Dana Reyes"),
            Snapshot(assigneeId: Sam, assigneeName: "Dana Reyes"));

        changes.ShouldHaveSingleItem().Kind.ShouldBe(TicketChangeKind.Assignment);
    }

    /// <summary>Recording a resolution writes its own line.</summary>
    [Fact]
    public void Recording_a_resolution_is_its_own_line()
    {
        var changes = TicketChanges.Between(
            Snapshot(resolutionNotes: null),
            Snapshot(resolutionNotes: "Replaced the charger."));

        var change = changes.ShouldHaveSingleItem();
        change.Kind.ShouldBe(TicketChangeKind.Resolution);
        change.From.ShouldBeNull();
        change.To.ShouldBe("Replaced the charger.");
    }

    /// <summary>
    /// Resolving writes two lines — the status moved and the work was documented — and
    /// that is not a duplicate.
    /// </summary>
    /// <remarks>
    /// A timeline showing only the status move has lost the sentence a technician actually
    /// wants to read, and one showing only the notes cannot say the ticket left In Progress.
    /// </remarks>
    [Fact]
    public void Resolving_writes_both_the_status_move_and_the_resolution()
    {
        var changes = TicketChanges.Between(
            Snapshot(status: TicketStatus.InProgress, resolutionNotes: null),
            Snapshot(status: TicketStatus.Resolved, resolutionNotes: "Replaced the charger."));

        changes.Count.ShouldBe(2);
        changes[0].Kind.ShouldBe(TicketChangeKind.Status);
        changes[1].Kind.ShouldBe(TicketChangeKind.Resolution);
    }

    /// <summary>
    /// Reopening writes the status move alone, because WP-1.3 keeps the notes the requester
    /// rejected rather than clearing them.
    /// </summary>
    [Fact]
    public void Reopening_writes_the_status_move_alone()
    {
        var changes = TicketChanges.Between(
            Snapshot(status: TicketStatus.Resolved, resolutionNotes: "Replaced the charger."),
            Snapshot(status: TicketStatus.InProgress, resolutionNotes: "Replaced the charger."));

        changes.ShouldHaveSingleItem().Kind.ShouldBe(TicketChangeKind.Status);
    }

    /// <summary>Every dimension moving at once writes one line each, in timeline order.</summary>
    [Fact]
    public void Four_moves_write_four_lines_in_a_fixed_order()
    {
        var changes = TicketChanges.Between(
            Snapshot(TicketStatus.InProgress, Medium, Dana, "Dana Reyes", null),
            Snapshot(TicketStatus.Resolved, Critical, Sam, "Sam Torres", "Replaced the charger."),
            new TicketPriorityNames("Medium", "Critical"));

        changes.Select(change => change.Kind).ShouldBe(
        [
            TicketChangeKind.Status,
            TicketChangeKind.Priority,
            TicketChangeKind.Assignment,
            TicketChangeKind.Resolution,
        ]);
    }

    /// <summary>An entry carries the actor, the instant, and the ticket it belongs to.</summary>
    [Fact]
    public void An_entry_records_who_when_and_which_ticket()
    {
        var ticketId = Guid.CreateVersion7();

        var entry = TicketHistoryEntry.Record(
            ticketId,
            new TicketChange(TicketChangeKind.Status, "New", "Assigned"),
            sequence: 0,
            Now,
            Dana,
            "Dana Reyes");

        entry.TicketId.ShouldBe(ticketId);
        entry.Kind.ShouldBe(TicketChangeKind.Status);
        entry.FromValue.ShouldBe("New");
        entry.ToValue.ShouldBe("Assigned");
        entry.OccurredAt.ShouldBe(Now);
        entry.Sequence.ShouldBe(0);
        entry.ActorId.ShouldBe(Dana);
        entry.ActorName.ShouldBe("Dana Reyes");
        entry.Id.ShouldNotBe(Guid.Empty);
    }

    /// <summary>A change the system made records no actor rather than inventing one.</summary>
    [Fact]
    public void An_entry_the_system_made_has_no_actor()
    {
        var entry = TicketHistoryEntry.Record(
            Guid.CreateVersion7(),
            new TicketChange(TicketChangeKind.Status, "New", "Cancelled"),
            sequence: 0,
            Now,
            actorId: null,
            actorName: null);

        entry.ActorId.ShouldBeNull();
        entry.ActorName.ShouldBeNull();
    }

    /// <summary>An entry with no ticket to belong to is refused.</summary>
    [Fact]
    public void An_entry_must_belong_to_a_ticket()
    {
        Should.Throw<ArgumentException>(() => TicketHistoryEntry.Record(
            Guid.Empty,
            new TicketChange(TicketChangeKind.Status, "New", "Assigned"),
            sequence: 0,
            Now,
            actorId: null,
            actorName: null));
    }

    /// <summary>
    /// An over-long value is capped rather than rejected, so it can never stop the change
    /// it describes from being recorded at all.
    /// </summary>
    [Fact]
    public void An_over_long_value_is_truncated_not_refused()
    {
        var tooLong = new string('x', TicketHistoryEntry.ValueMaxLength + 50);

        var entry = TicketHistoryEntry.Record(
            Guid.CreateVersion7(),
            new TicketChange(TicketChangeKind.Resolution, null, tooLong),
            sequence: 0,
            Now,
            actorId: null,
            actorName: new string('y', TicketHistoryEntry.ActorNameMaxLength + 10));

        entry.ToValue!.Length.ShouldBe(TicketHistoryEntry.ValueMaxLength);
        entry.ActorName!.Length.ShouldBe(TicketHistoryEntry.ActorNameMaxLength);
    }

    /// <summary>
    /// Two lines from one change are told apart by their ordinal, because they share an
    /// instant and a version 7 id is random within a millisecond.
    /// </summary>
    [Fact]
    public void Lines_from_one_change_are_ordered_by_their_ordinal()
    {
        var ticketId = Guid.CreateVersion7();
        var changes = TicketChanges.Between(
            Snapshot(status: TicketStatus.InProgress, resolutionNotes: null),
            Snapshot(status: TicketStatus.Resolved, resolutionNotes: "Replaced the charger."));

        var entries = changes
            .Select((change, sequence) => TicketHistoryEntry.Record(ticketId, change, sequence, Now, Dana, "Dana Reyes"))
            .ToList();

        entries.Select(entry => entry.Sequence).ShouldBe([0, 1]);
        entries.Select(entry => entry.OccurredAt).Distinct().ShouldHaveSingleItem();
    }

    /// <summary>A negative ordinal is refused: there is no line before the first one.</summary>
    [Fact]
    public void An_ordinal_cannot_be_negative()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => TicketHistoryEntry.Record(
            Guid.CreateVersion7(),
            new TicketChange(TicketChangeKind.Status, "New", "Assigned"),
            sequence: -1,
            Now,
            actorId: null,
            actorName: null));
    }

    /// <summary>A snapshot reads the four tracked dimensions off a real ticket.</summary>
    /// <remarks>
    /// The one place the two halves meet: if <see cref="Ticket"/> ever renames or drops one
    /// of these, this stops compiling rather than silently recording less.
    /// </remarks>
    [Fact]
    public void A_snapshot_reads_the_tracked_dimensions_off_a_ticket()
    {
        var draft = new NewTicket(
            "Laptop will not charge",
            "It stops charging at 40%.",
            Guid.CreateVersion7(),
            "Dana Reyes",
            Guid.CreateVersion7(),
            "Water Operations",
            Guid.CreateVersion7(),
            Medium,
            new SlaTargets(30, 240));

        var ticket = Ticket.Create("TKT-0001", draft, Now, Dana);

        var snapshot = TicketSnapshot.Of(ticket);

        snapshot.Status.ShouldBe(TicketStatus.New);
        snapshot.PriorityId.ShouldBe(Medium);
        snapshot.AssigneeId.ShouldBeNull();
        snapshot.AssigneeName.ShouldBeNull();
        snapshot.ResolutionNotes.ShouldBeNull();
    }
}
