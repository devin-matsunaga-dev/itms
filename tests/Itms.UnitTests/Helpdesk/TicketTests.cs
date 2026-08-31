using Itms.Modules.Helpdesk.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// The ticket entity at creation. Invariant 1 — a ticket always has a requester, a
/// category, a priority, and a status — lives here, and this is where it is asserted.
/// </summary>
/// <remarks>
/// There are no transition tests in this file on purpose: WP-1.2 gives the entity no
/// behaviour past creation, and WP-1.3's state machine brings its own suite.
/// </remarks>
public sealed class TicketTests
{
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly Guid Requester = Guid.CreateVersion7();
    private static readonly Guid Department = Guid.CreateVersion7();
    private static readonly Guid Category = Guid.CreateVersion7();
    private static readonly Guid Priority = Guid.CreateVersion7();

    /// <summary>
    /// The seeded Medium priority's targets — thirty minutes to respond, four hours to
    /// resolve. WP-1.8 made a pair of them part of every draft.
    /// </summary>
    private static readonly SlaTargets Targets = new(30, 240);

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_new_ticket_carries_everything_invariant_1_requires()
    {
        var ticket = Ticket.Create("TKT-0001", Draft(), _clock.UtcNow, Actor);

        ticket.Id.ShouldNotBe(Guid.Empty);
        ticket.Number.ShouldBe("TKT-0001");
        ticket.RequesterId.ShouldBe(Requester);
        ticket.DepartmentId.ShouldBe(Department);
        ticket.CategoryId.ShouldBe(Category);
        ticket.PriorityId.ShouldBe(Priority);
        ticket.Status.ShouldBe(TicketStatus.New);
    }

    /// <summary>
    /// The caller does not choose the starting state. Letting one would be the first way
    /// around the state machine WP-1.3 puts in front of every other transition.
    /// </summary>
    [Fact]
    public void Every_ticket_starts_at_New() =>
        Ticket.Create("TKT-0007", Draft(), _clock.UtcNow, Actor).Status.ShouldBe(TicketStatus.New);

    [Fact]
    public void A_new_ticket_is_stamped_with_the_clock_and_the_actor()
    {
        var ticket = Ticket.Create("TKT-0001", Draft(), _clock.UtcNow, Actor);

        ticket.CreatedAt.ShouldBe(_clock.UtcNow);
        ticket.CreatedBy.ShouldBe(Actor);
        ticket.UpdatedAt.ShouldBe(_clock.UtcNow);
        ticket.UpdatedBy.ShouldBe(Actor);
    }

    /// <summary>
    /// Every field SPEC.md §2 names but no package before WP-1.3 fills. They exist so the
    /// packages that own them add a method rather than a migration; a new ticket must not
    /// arrive with any of them already set.
    /// </summary>
    /// <remarks>
    /// <c>DueAt</c> left this list at WP-1.8, which is the package that owns it: a ticket
    /// now arrives with both SLA clocks already running, and an empty due date would mean a
    /// ticket nobody had promised anything about.
    /// </remarks>
    [Fact]
    public void A_new_ticket_is_unassigned_unresolved_unclosed_and_undeleted()
    {
        var ticket = Ticket.Create("TKT-0001", Draft(), _clock.UtcNow, Actor);

        ticket.AssigneeId.ShouldBeNull();
        ticket.AssigneeName.ShouldBeNull();
        ticket.RelatedAssetId.ShouldBeNull();
        ticket.RelatedAlertId.ShouldBeNull();
        ticket.ResolutionNotes.ShouldBeNull();
        ticket.ResolvedAt.ShouldBeNull();
        ticket.ClosedAt.ShouldBeNull();
        ticket.DeletedAt.ShouldBeNull();
    }

    /// <summary>
    /// §3 rule 6: the requester and the department are ids from other modules, so their
    /// names are copied onto the row rather than joined to.
    /// </summary>
    [Fact]
    public void The_cross_module_display_names_are_cached_on_the_row()
    {
        var ticket = Ticket.Create("TKT-0001", Draft(), _clock.UtcNow, Actor);

        ticket.RequesterName.ShouldBe("Dana Reyes");
        ticket.DepartmentName.ShouldBe("Water Operations");
    }

    [Fact]
    public void Text_is_trimmed()
    {
        var ticket = Ticket.Create(
            "TKT-0001",
            Draft() with { Subject = "  Laptop will not charge  ", Description = " Since Monday. " },
            _clock.UtcNow,
            Actor);

        ticket.Subject.ShouldBe("Laptop will not charge");
        ticket.Description.ShouldBe("Since Monday.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0001")]
    [InlineData("INC-0001")]
    public void A_ticket_cannot_be_created_with_a_number_it_could_not_have_been_issued(string number) =>
        Should.Throw<ArgumentException>(() => Ticket.Create(number, Draft(), _clock.UtcNow, Actor));

    [Fact]
    public void A_ticket_cannot_be_created_without_a_requester() =>
        Should.Throw<ArgumentException>(
            () => Ticket.Create("TKT-0001", Draft() with { RequesterId = Guid.Empty }, _clock.UtcNow, Actor));

    [Fact]
    public void A_ticket_cannot_be_created_without_a_department() =>
        Should.Throw<ArgumentException>(
            () => Ticket.Create("TKT-0001", Draft() with { DepartmentId = Guid.Empty }, _clock.UtcNow, Actor));

    [Fact]
    public void A_ticket_cannot_be_created_without_a_category() =>
        Should.Throw<ArgumentException>(
            () => Ticket.Create("TKT-0001", Draft() with { CategoryId = Guid.Empty }, _clock.UtcNow, Actor));

    [Fact]
    public void A_ticket_cannot_be_created_without_a_priority() =>
        Should.Throw<ArgumentException>(
            () => Ticket.Create("TKT-0001", Draft() with { PriorityId = Guid.Empty }, _clock.UtcNow, Actor));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_ticket_cannot_be_created_without_a_subject(string subject) =>
        Should.Throw<ArgumentException>(
            () => Ticket.Create("TKT-0001", Draft() with { Subject = subject }, _clock.UtcNow, Actor));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_ticket_cannot_be_created_without_a_description(string description) =>
        Should.Throw<ArgumentException>(
            () => Ticket.Create("TKT-0001", Draft() with { Description = description }, _clock.UtcNow, Actor));

    /// <summary>
    /// The bound matters because the column carries it: text longer than the column would
    /// otherwise fail at the database with a message nobody can act on.
    /// </summary>
    [Fact]
    public void A_subject_longer_than_its_column_is_refused() =>
        Should.Throw<ArgumentException>(
            () => Ticket.Create(
                "TKT-0001",
                Draft() with { Subject = new string('x', Ticket.SubjectMaxLength + 1) },
                _clock.UtcNow,
                Actor));

    [Fact]
    public void A_description_longer_than_its_column_is_refused() =>
        Should.Throw<ArgumentException>(
            () => Ticket.Create(
                "TKT-0001",
                Draft() with { Description = new string('x', Ticket.DescriptionMaxLength + 1) },
                _clock.UtcNow,
                Actor));

    [Fact]
    public void A_cached_display_name_longer_than_its_column_is_refused() =>
        Should.Throw<ArgumentException>(
            () => Ticket.Create(
                "TKT-0001",
                Draft() with { RequesterName = new string('x', Ticket.DisplayNameMaxLength + 1) },
                _clock.UtcNow,
                Actor));

    /// <summary>The system raising a ticket has no actor, and that is recorded as null rather than invented.</summary>
    [Fact]
    public void A_ticket_raised_by_the_system_has_no_actor()
    {
        var ticket = Ticket.Create("TKT-0001", Draft(), _clock.UtcNow, actor: null);

        ticket.CreatedBy.ShouldBeNull();
        ticket.UpdatedBy.ShouldBeNull();
    }

    private static NewTicket Draft() => new(
        "Laptop will not charge",
        "It stops charging at 40% and the light goes amber.",
        Requester,
        "Dana Reyes",
        Department,
        "Water Operations",
        Category,
        Priority,
        Targets);
}
