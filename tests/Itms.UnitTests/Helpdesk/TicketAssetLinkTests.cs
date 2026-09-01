using Itms.Modules.Helpdesk.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// <see cref="Ticket.LinkAsset"/> — the entity half of WP-2.5's ticket ↔ asset join.
/// </summary>
/// <remarks>
/// <para>
/// The rule these exist to make true is that <b>a link change is always a change</b>:
/// naming the asset a ticket already names, or clearing a link it does not have, is
/// refused rather than treated as a no-op. That matters because the timeline is built by
/// diffing two snapshots — a call that wrote the same value back would either produce a
/// line describing a change that did not happen, or produce nothing while answering 200
/// as though it had.
/// </para>
/// <para>
/// Whether the asset exists is a fact about Assets' rows that this entity cannot read.
/// <c>TicketAssetLinkEndpointTests</c> covers it against the real lookup.
/// </para>
/// </remarks>
public sealed class TicketAssetLinkTests
{
    private static readonly Guid Author = Guid.CreateVersion7();
    private static readonly Guid Laptop = Guid.CreateVersion7();
    private static readonly Guid Printer = Guid.CreateVersion7();

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_new_ticket_names_no_asset()
    {
        NewTicket().RelatedAssetId.ShouldBeNull();
    }

    [Fact]
    public void Linking_an_asset_records_it_and_stamps_the_actor()
    {
        var ticket = NewTicket();
        _clock.Advance(TimeSpan.FromMinutes(20));

        ticket.LinkAsset(Laptop, _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.RelatedAssetId.ShouldBe(Laptop);
        ticket.UpdatedAt.ShouldBe(_clock.UtcNow);
        ticket.UpdatedBy.ShouldBe(Author);
    }

    /// <summary>Relinking is one call, not an unlink followed by a link.</summary>
    [Fact]
    public void Relinking_replaces_the_asset_the_ticket_names()
    {
        var ticket = NewTicket();
        ticket.LinkAsset(Laptop, _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.LinkAsset(Printer, _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.RelatedAssetId.ShouldBe(Printer);
    }

    [Fact]
    public void Passing_null_clears_the_link()
    {
        var ticket = NewTicket();
        ticket.LinkAsset(Laptop, _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.LinkAsset(null, _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.RelatedAssetId.ShouldBeNull();
    }

    /// <summary>
    /// Refused rather than accepted silently, so the timeline cannot gain a line saying the
    /// ticket moved from an asset to the same asset.
    /// </summary>
    [Fact]
    public void Linking_the_asset_the_ticket_already_names_is_refused()
    {
        var ticket = NewTicket();
        ticket.LinkAsset(Laptop, _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        var result = ticket.LinkAsset(Laptop, _clock.UtcNow, Author);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.already_linked_to_that_asset");
        ticket.RelatedAssetId.ShouldBe(Laptop);
    }

    [Fact]
    public void Clearing_a_link_the_ticket_does_not_have_is_refused()
    {
        var ticket = NewTicket();

        var result = ticket.LinkAsset(null, _clock.UtcNow, Author);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.ticket_has_no_related_asset");
    }

    /// <summary>
    /// A closed or cancelled ticket is a finished record. A resolved one is not terminal
    /// and stays linkable, which is deliberate: the asset is very often identified while
    /// the resolution is being written up.
    /// </summary>
    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void A_terminal_ticket_cannot_be_relinked(TicketStatus status)
    {
        var ticket = ParkedAt(status);

        var result = ticket.LinkAsset(Laptop, _clock.UtcNow, Author);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("helpdesk.ticket_not_linkable");
        ticket.RelatedAssetId.ShouldBeNull();
    }

    [Fact]
    public void A_resolved_ticket_can_still_be_linked()
    {
        var ticket = ParkedAt(TicketStatus.Resolved);

        ticket.LinkAsset(Laptop, _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.RelatedAssetId.ShouldBe(Laptop);
    }

    /// <summary>
    /// An empty guid is a client bug, and reading it as "unlink" would carry out an
    /// instruction nobody gave. The validator refuses it first; the entity refuses it as
    /// well, because the validator is not the only door.
    /// </summary>
    [Fact]
    public void An_empty_asset_id_is_rejected_rather_than_read_as_an_unlink()
    {
        var ticket = NewTicket();
        ticket.LinkAsset(Laptop, _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        Should.Throw<ArgumentException>(() => ticket.LinkAsset(Guid.Empty, _clock.UtcNow, Author));

        ticket.RelatedAssetId.ShouldBe(Laptop);
    }

    /// <summary>The link never touches the workflow. It says what the ticket is about, not where it is.</summary>
    [Fact]
    public void Linking_does_not_move_the_status()
    {
        var ticket = NewTicket();

        ticket.LinkAsset(Laptop, _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

        ticket.Status.ShouldBe(TicketStatus.New);
    }

    /// <summary>
    /// A ticket walked to <paramref name="status"/> through the real transitions.
    /// </summary>
    /// <remarks>
    /// Walked rather than set, because <c>Status</c> has no setter and the state machine is
    /// the only way through it — the same route every other domain test takes. Reaching
    /// <c>Assigned</c> goes through <see cref="Ticket.Assign"/> rather than
    /// <c>ChangeStatus</c>, because that edge names somebody and the two writes happen
    /// together.
    /// </remarks>
    /// <param name="status">Where the ticket should end up.</param>
    private Ticket ParkedAt(TicketStatus status)
    {
        var ticket = NewTicket();

        if (status == TicketStatus.Cancelled)
        {
            ticket.ChangeStatus(TicketStatus.Cancelled, null, null, _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();
            return ticket;
        }

        ticket.Assign(Guid.CreateVersion7(), "Priya Raman", _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();
        Move(ticket, TicketStatus.InProgress);
        Move(ticket, TicketStatus.Resolved, "Replaced the charger.");

        if (status == TicketStatus.Closed)
        {
            Move(ticket, TicketStatus.Closed);
        }

        return ticket;
    }

    private void Move(Ticket ticket, TicketStatus target, string? resolutionNotes = null) =>
        ticket.ChangeStatus(target, resolutionNotes, null, _clock.UtcNow, Author).IsSuccess.ShouldBeTrue();

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
            Targets),
        _clock.UtcNow,
        Author);

    private static SlaTargets Targets => new(30, 240);
}
