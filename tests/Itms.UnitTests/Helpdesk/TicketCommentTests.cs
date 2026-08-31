using Itms.Modules.Helpdesk.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// The comment entity: what it insists on at the moment it is written, and what it
/// deliberately offers no way to change afterwards.
/// </summary>
public sealed class TicketCommentTests
{
    private static readonly Guid Ticket = Guid.CreateVersion7();
    private static readonly Guid Author = Guid.CreateVersion7();

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_posted_comment_carries_its_ticket_author_body_and_instant()
    {
        var comment = TicketComment.Post(Ticket, "The printer is jammed again.", false, Author, "Ada Lovelace", _clock.UtcNow);

        comment.Id.ShouldNotBe(Guid.Empty);
        comment.TicketId.ShouldBe(Ticket);
        comment.Body.ShouldBe("The printer is jammed again.");
        comment.AuthorId.ShouldBe(Author);
        comment.AuthorName.ShouldBe("Ada Lovelace");
        comment.CreatedAt.ShouldBe(_clock.UtcNow);
    }

    /// <summary>
    /// The default audience is the requester's. A comment that has to be marked public to
    /// be public would hide somebody's reply the first time a client forgot the field.
    /// </summary>
    [Fact]
    public void A_comment_is_public_unless_it_is_asked_to_be_internal()
    {
        TicketComment.Post(Ticket, "Replaced the toner.", false, Author, "Ada", _clock.UtcNow)
            .IsInternal.ShouldBeFalse();

        TicketComment.Post(Ticket, "Third failure this month, replace the unit.", true, Author, "Ada", _clock.UtcNow)
            .IsInternal.ShouldBeTrue();
    }

    /// <summary>
    /// Trailing whitespace is not content. Trimming here rather than at the edge means the
    /// length rule below measures what is actually stored.
    /// </summary>
    [Fact]
    public void A_body_is_trimmed_before_it_is_stored()
    {
        TicketComment.Post(Ticket, "   Looked at it.\n  ", false, Author, "Ada", _clock.UtcNow)
            .Body.ShouldBe("Looked at it.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void A_blank_body_is_not_a_comment(string body) =>
        Should.Throw<ArgumentException>(() =>
            TicketComment.Post(Ticket, body, false, Author, "Ada", _clock.UtcNow));

    [Fact]
    public void A_body_longer_than_the_column_is_refused_rather_than_silently_cut() =>
        Should.Throw<ArgumentOutOfRangeException>(() => TicketComment.Post(
            Ticket,
            new string('x', TicketComment.BodyMaxLength + 1),
            false,
            Author,
            "Ada",
            _clock.UtcNow));

    /// <summary>
    /// A body exactly at the limit is inside it. Off-by-one at a boundary a validator also
    /// checks is how the two end up disagreeing.
    /// </summary>
    [Fact]
    public void A_body_exactly_at_the_limit_is_accepted() =>
        TicketComment.Post(
            Ticket,
            new string('x', TicketComment.BodyMaxLength),
            false,
            Author,
            "Ada",
            _clock.UtcNow).Body.Length.ShouldBe(TicketComment.BodyMaxLength);

    /// <summary>
    /// The cached name is truncated rather than refused, following the history entry: an
    /// over-long display name must never stop the thing it describes from being recorded.
    /// </summary>
    [Fact]
    public void An_over_long_author_name_is_truncated_not_refused() =>
        TicketComment.Post(
            Ticket,
            "Noted.",
            false,
            Author,
            new string('n', TicketComment.AuthorNameMaxLength + 50),
            _clock.UtcNow).AuthorName.Length.ShouldBe(TicketComment.AuthorNameMaxLength);

    [Fact]
    public void A_comment_must_belong_to_a_ticket() =>
        Should.Throw<ArgumentException>(() =>
            TicketComment.Post(Guid.Empty, "Orphan.", false, Author, "Ada", _clock.UtcNow));

    [Fact]
    public void A_comment_must_have_an_author() =>
        Should.Throw<ArgumentException>(() =>
            TicketComment.Post(Ticket, "Anonymous.", false, Guid.Empty, "Ada", _clock.UtcNow));

    /// <summary>
    /// Write-once is a property of the type, not a convention somebody remembers. If a
    /// setter ever appears — in particular one that could publish an internal note to the
    /// requester — this is what says so.
    /// </summary>
    [Fact]
    public void A_comment_exposes_no_way_to_change_what_was_said_or_who_may_read_it() =>
        typeof(TicketComment)
            .GetProperties()
            .Where(property => property.SetMethod is { IsPublic: true })
            .ShouldBeEmpty();
}
