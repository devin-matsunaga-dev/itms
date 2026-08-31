using Itms.Modules.Helpdesk.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// The SLA half of <see cref="Ticket"/>: what a transition does to the two clocks, and
/// what they read afterwards.
/// </summary>
/// <remarks>
/// <para>
/// Every instant here comes from a <see cref="FakeClock"/> the test moves by hand, which is
/// what CONVENTIONS.md's ban on <c>Thread.Sleep</c> is for — a pause of two hours is two
/// hours of <see cref="FakeClock.Advance"/> and takes no time at all.
/// </para>
/// <para>
/// The arithmetic itself is exhausted in <see cref="SlaClockTests"/>. What is asserted here
/// is that the entity applies it: that the clocks move when the ticket does, and that no
/// handler has to remember to move them.
/// </para>
/// </remarks>
public sealed class TicketSlaTests
{
    private static readonly Guid Author = Guid.CreateVersion7();
    private static readonly Guid Technician = Guid.CreateVersion7();

    /// <summary>The seeded Medium priority: thirty minutes to respond, four hours to resolve.</summary>
    private static readonly SlaTargets Medium = new(30, 240);

    /// <summary>The seeded Critical priority: fifteen minutes to respond, two hours to resolve.</summary>
    private static readonly SlaTargets Critical = new(15, 120);

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_new_ticket_starts_both_clocks_at_its_creation_instant()
    {
        var created = _clock.UtcNow;
        var ticket = NewTicket();

        ticket.ResponseTargetMinutes.ShouldBe(30);
        ticket.ResolutionTargetMinutes.ShouldBe(240);
        ticket.ResponseDueAt.ShouldBe(created.AddMinutes(30));
        ticket.ResponseWarnAt.ShouldBe(created.AddMinutes(24));
        ticket.DueAt.ShouldBe(created.AddMinutes(240));
        ticket.ResolutionWarnAt.ShouldBe(created.AddMinutes(192));
        ticket.RespondedAt.ShouldBeNull();
        ticket.SlaPausedAt.ShouldBeNull();
        ticket.SlaPausedTotal.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void A_ticket_that_has_only_been_assigned_and_started_has_not_been_responded_to()
    {
        var ticket = InProgress();

        // Assignment is not a response, at the human's direction: being handed a ticket is
        // not answering the person who raised it.
        ticket.RespondedAt.ShouldBeNull();
    }

    [Fact]
    public void Entering_Waiting_parks_the_resolution_clock_and_leaves_the_deadline_where_it_was()
    {
        var ticket = InProgress();
        var due = ticket.DueAt;
        var warn = ticket.ResolutionWarnAt;

        _clock.Advance(TimeSpan.FromMinutes(30));
        var parked = _clock.UtcNow;

        ticket.Wait(_clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();

        ticket.SlaPausedAt.ShouldBe(parked);
        ticket.DueAt.ShouldBe(due);
        ticket.ResolutionWarnAt.ShouldBe(warn);
    }

    [Fact]
    public void Leaving_Waiting_pushes_both_resolution_instants_forward_by_the_pause()
    {
        var ticket = InProgress();
        var due = ticket.DueAt;
        var warn = ticket.ResolutionWarnAt;

        ticket.Wait(_clock.UtcNow, Technician);
        _clock.Advance(TimeSpan.FromHours(3));
        ticket.Resume(_clock.UtcNow, Technician);

        ticket.SlaPausedAt.ShouldBeNull();
        ticket.SlaPausedTotal.ShouldBe(TimeSpan.FromHours(3));
        ticket.DueAt.ShouldBe(due.AddHours(3));
        ticket.ResolutionWarnAt.ShouldBe(warn.AddHours(3));
    }

    /// <summary>
    /// WP-1.8's criterion names "pause/resume across multiple Waiting periods". Three of
    /// them, with work between, and the deadline moves by their sum and nothing else.
    /// </summary>
    [Fact]
    public void Several_Waiting_periods_accumulate()
    {
        var ticket = InProgress();
        var due = ticket.DueAt;

        foreach (var hours in new[] { 1, 2, 4 })
        {
            _clock.Advance(TimeSpan.FromMinutes(5));
            ticket.Wait(_clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();

            _clock.Advance(TimeSpan.FromHours(hours));
            ticket.Resume(_clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();
        }

        ticket.SlaPausedTotal.ShouldBe(TimeSpan.FromHours(7));
        ticket.DueAt.ShouldBe(due.AddHours(7));
    }

    /// <summary>
    /// SPEC.md §2 pauses the resolution clock and says nothing about the response one, so
    /// a ticket parked before anybody replied keeps running out of time to reply.
    /// </summary>
    [Fact]
    public void A_pause_does_not_move_the_response_clock()
    {
        var ticket = InProgress();
        var responseDue = ticket.ResponseDueAt;
        var responseWarn = ticket.ResponseWarnAt;

        ticket.Wait(_clock.UtcNow, Technician);
        _clock.Advance(TimeSpan.FromHours(6));
        ticket.Resume(_clock.UtcNow, Technician);

        ticket.ResponseDueAt.ShouldBe(responseDue);
        ticket.ResponseWarnAt.ShouldBe(responseWarn);
    }

    [Fact]
    public void A_ticket_parked_in_Waiting_cannot_drift_into_a_breach()
    {
        var ticket = InProgress();

        ticket.Wait(_clock.UtcNow, Technician);

        // A week goes by with nobody able to work on it.
        _clock.Advance(TimeSpan.FromDays(7));

        var assessment = SlaAssessment.Of(ticket.Sla, ticket.Status, _clock.UtcNow);

        assessment.Resolution.ShouldBe(SlaState.Pending);
        assessment.IsPaused.ShouldBeTrue();
    }

    /// <summary>
    /// Parked is not the same as safe. A ticket that had already run out of time before it
    /// was parked still reads as breached — which is why <c>IsPaused</c> is a fact
    /// alongside the state rather than a value of it.
    /// </summary>
    [Fact]
    public void A_ticket_parked_after_it_had_already_breached_still_reads_as_breached()
    {
        var ticket = InProgress();

        _clock.Advance(TimeSpan.FromHours(5));
        ticket.Wait(_clock.UtcNow, Technician);
        _clock.Advance(TimeSpan.FromDays(2));

        var assessment = SlaAssessment.Of(ticket.Sla, ticket.Status, _clock.UtcNow);

        assessment.Resolution.ShouldBe(SlaState.Breached);
        assessment.IsPaused.ShouldBeTrue();
    }

    [Fact]
    public void Resolving_from_Waiting_closes_the_pause_first()
    {
        var ticket = InProgress();
        var due = ticket.DueAt;

        ticket.Wait(_clock.UtcNow, Technician);
        _clock.Advance(TimeSpan.FromHours(2));
        ticket.Resolve("Replaced the charger.", _clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();

        ticket.SlaPausedAt.ShouldBeNull();
        ticket.SlaPausedTotal.ShouldBe(TimeSpan.FromHours(2));
        ticket.DueAt.ShouldBe(due.AddHours(2));
        SlaAssessment.Of(ticket.Sla, ticket.Status, _clock.UtcNow).Resolution.ShouldBe(SlaState.Met);
    }

    [Fact]
    public void Resolving_stops_the_response_clock_when_nothing_else_has()
    {
        var ticket = InProgress();

        _clock.Advance(TimeSpan.FromHours(1));
        ticket.Resolve("Replaced the charger.", _clock.UtcNow, Technician);

        ticket.RespondedAt.ShouldBe(_clock.UtcNow);

        // An hour, against a thirty-minute response target.
        SlaAssessment.Of(ticket.Sla, ticket.Status, _clock.UtcNow).Response.ShouldBe(SlaState.Breached);
    }

    [Fact]
    public void A_response_already_recorded_is_not_moved_by_the_resolution()
    {
        var ticket = InProgress();

        _clock.Advance(TimeSpan.FromMinutes(10));
        var answered = _clock.UtcNow;
        ticket.RecordResponse(answered).ShouldBeTrue();

        _clock.Advance(TimeSpan.FromHours(1));
        ticket.Resolve("Replaced the charger.", _clock.UtcNow, Technician);

        ticket.RespondedAt.ShouldBe(answered);
        SlaAssessment.Of(ticket.Sla, ticket.Status, _clock.UtcNow).Response.ShouldBe(SlaState.Met);
    }

    [Fact]
    public void A_second_response_is_ignored_rather_than_refused()
    {
        var ticket = InProgress();
        var answered = _clock.UtcNow;

        ticket.RecordResponse(answered).ShouldBeTrue();

        _clock.Advance(TimeSpan.FromMinutes(5));

        ticket.RecordResponse(_clock.UtcNow).ShouldBeFalse();
        ticket.RespondedAt.ShouldBe(answered);
    }

    /// <summary>
    /// Time spent Resolved is not a pause — that was settled at the human's direction, on
    /// the grounds that SPEC.md pauses for Waiting and nothing else. A resolution that sat
    /// uncontested past its target and is then rejected comes back already breached.
    /// </summary>
    [Fact]
    public void A_reopened_ticket_can_come_back_already_breached()
    {
        var ticket = InProgress();

        _clock.Advance(TimeSpan.FromHours(1));
        ticket.Resolve("Replaced the charger.", _clock.UtcNow, Technician);
        SlaAssessment.Of(ticket.Sla, ticket.Status, _clock.UtcNow).Resolution.ShouldBe(SlaState.Met);

        _clock.Advance(TimeSpan.FromHours(6));
        ticket.Reopen(_clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();

        ticket.ResolvedAt.ShouldBeNull();
        ticket.SlaPausedTotal.ShouldBe(TimeSpan.Zero);
        SlaAssessment.Of(ticket.Sla, ticket.Status, _clock.UtcNow).Resolution.ShouldBe(SlaState.Breached);

        // The response happened; reopening does not unmake it.
        ticket.RespondedAt.ShouldNotBeNull();
    }

    [Fact]
    public void A_cancelled_ticket_has_no_SLA_outcome()
    {
        var ticket = InProgress();

        _clock.Advance(TimeSpan.FromDays(3));

        // Overdue by two and a half days at the moment it is abandoned.
        SlaAssessment.Of(ticket.Sla, ticket.Status, _clock.UtcNow).Resolution.ShouldBe(SlaState.Breached);

        ticket.Cancel(_clock.UtcNow, Technician).IsSuccess.ShouldBeTrue();

        var assessment = SlaAssessment.Of(ticket.Sla, ticket.Status, _clock.UtcNow);

        assessment.Resolution.ShouldBe(SlaState.Stopped);
        assessment.Response.ShouldBe(SlaState.Stopped);
        assessment.IsPaused.ShouldBeFalse();
    }

    /// <summary>
    /// WP-1.8's criterion names "priority changes mid-flight". Both deadlines are re-cut
    /// from creation, because SPEC.md §2 measures a target against the creation instant —
    /// so a ticket promoted two hours in is a Critical ticket that was raised two hours
    /// ago, not one whose two hours start now.
    /// </summary>
    [Fact]
    public void Retargeting_re_cuts_both_deadlines_from_creation()
    {
        var ticket = InProgress();
        var created = ticket.CreatedAt;

        _clock.Advance(TimeSpan.FromHours(2));
        ticket.RetargetSla(Critical, _clock.UtcNow, Technician);

        ticket.ResponseTargetMinutes.ShouldBe(15);
        ticket.ResolutionTargetMinutes.ShouldBe(120);
        ticket.ResponseDueAt.ShouldBe(created.AddMinutes(15));
        ticket.ResponseWarnAt.ShouldBe(created.AddMinutes(12));
        ticket.DueAt.ShouldBe(created.AddMinutes(120));
        ticket.ResolutionWarnAt.ShouldBe(created.AddMinutes(96));
    }

    [Fact]
    public void Retargeting_a_long_running_ticket_can_breach_it_immediately()
    {
        var ticket = InProgress();

        // Past 80% of the four-hour target — 192 minutes — and well past Critical's whole two hours.
        _clock.Advance(TimeSpan.FromMinutes(200));
        SlaAssessment.Of(ticket.Sla, ticket.Status, _clock.UtcNow).Resolution.ShouldBe(SlaState.Approaching);

        ticket.RetargetSla(Critical, _clock.UtcNow, Technician);

        SlaAssessment.Of(ticket.Sla, ticket.Status, _clock.UtcNow).Resolution.ShouldBe(SlaState.Breached);
    }

    /// <summary>
    /// The pause accounting survives a retarget: the ticket really did spend that time
    /// Waiting, whatever its priority was called.
    /// </summary>
    [Fact]
    public void Retargeting_keeps_the_pauses_already_accrued()
    {
        var ticket = InProgress();
        var created = ticket.CreatedAt;

        ticket.Wait(_clock.UtcNow, Technician);
        _clock.Advance(TimeSpan.FromHours(5));
        ticket.Resume(_clock.UtcNow, Technician);

        ticket.RetargetSla(Critical, _clock.UtcNow, Technician);

        ticket.SlaPausedTotal.ShouldBe(TimeSpan.FromHours(5));
        ticket.DueAt.ShouldBe(created.AddHours(5).AddMinutes(120));
        ticket.ResolutionWarnAt.ShouldBe(created.AddHours(5).AddMinutes(96));
    }

    /// <summary>
    /// A retarget in the middle of a pause takes only the pauses already closed. The one
    /// still running is added when it closes, which is what <see cref="Ticket.Resume"/>
    /// does to whatever deadline it finds.
    /// </summary>
    [Fact]
    public void Retargeting_while_parked_leaves_the_open_pause_to_the_resume()
    {
        var ticket = InProgress();
        var created = ticket.CreatedAt;

        ticket.Wait(_clock.UtcNow, Technician);
        _clock.Advance(TimeSpan.FromHours(2));
        ticket.RetargetSla(Critical, _clock.UtcNow, Technician);

        ticket.DueAt.ShouldBe(created.AddMinutes(120));

        _clock.Advance(TimeSpan.FromHours(1));
        ticket.Resume(_clock.UtcNow, Technician);

        ticket.SlaPausedTotal.ShouldBe(TimeSpan.FromHours(3));
        ticket.DueAt.ShouldBe(created.AddHours(3).AddMinutes(120));
    }

    [Fact]
    public void The_states_walk_pending_then_approaching_then_breached()
    {
        var ticket = InProgress();

        Resolution().ShouldBe(SlaState.Pending);

        // 192 minutes is 80% of the four-hour target.
        _clock.Advance(TimeSpan.FromMinutes(191));
        Resolution().ShouldBe(SlaState.Pending);

        _clock.Advance(TimeSpan.FromMinutes(1));
        Resolution().ShouldBe(SlaState.Approaching);

        _clock.Advance(TimeSpan.FromMinutes(48));
        Resolution().ShouldBe(SlaState.Breached);

        SlaState Resolution() => SlaAssessment.Of(ticket.Sla, ticket.Status, _clock.UtcNow).Resolution;
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 240)]
    [InlineData(30, 0)]
    [InlineData(240, 30)]
    [InlineData(365 * 24 * 60 + 1, 365 * 24 * 60 + 1)]
    public void A_target_a_clock_cannot_be_run_against_is_refused(int response, int resolution) =>
        Should.Throw<ArgumentOutOfRangeException>(
            () => TicketSla.Start(new SlaTargets(response, resolution), _clock.UtcNow));

    /// <summary>A ticket walked to In Progress through legal moves only.</summary>
    private Ticket InProgress()
    {
        var ticket = NewTicket();

        ticket.Assign(Technician, "Sam Okafor", _clock.UtcNow, Author);
        ticket.Start(_clock.UtcNow, Technician);

        return ticket;
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
            Guid.CreateVersion7(),
            Medium),
        _clock.UtcNow,
        Author);
}
