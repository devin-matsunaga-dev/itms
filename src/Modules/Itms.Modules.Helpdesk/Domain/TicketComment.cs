namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// Something somebody said about a ticket — either to the requester, or about the ticket
/// where the requester cannot see it (SPEC.md §2, "internal notes, user-visible comments").
/// </summary>
/// <remarks>
/// <para>
/// <b>One table, one flag, not two tables.</b> A note and a comment differ in exactly one
/// respect — who may read it — and everything else about them is identical: same author,
/// same body, same place in the thread, same ordering. Two tables would mean two
/// projections, two insert paths, and two chances for one of them to forget the audience
/// check. One table with <see cref="IsInternal"/> means the audience question is asked in
/// one place, <c>TicketVisibility</c>, and every read composes it.
/// </para>
/// <para>
/// <b>Write-once, like a history entry.</b> There is a factory and there are no mutators.
/// WP-1.7 was given no edit or delete path and did not invent one: a thread a technician
/// can silently rewrite is not a record of what was said, and on a boundary this sensitive
/// an affordance should be argued for rather than discovered. The consequence a later
/// package must accept is that a note posted as internal stays internal — there is no
/// method that publishes one to the requester, because doing so would show them text
/// written on the understanding that they would not see it.
/// </para>
/// <para>
/// <b>The author's name is cached, like every other display name on a ticket.</b> §3 rule
/// 6 forbids a foreign key across a module boundary, so the author is an id plus the name
/// they had at the time. It goes stale on a rename exactly as
/// <see cref="Ticket.RequesterName"/> does, and it is refreshed by the same event that
/// will one day refresh those — see STATUS.md.
/// </para>
/// </remarks>
public sealed class TicketComment
{
    /// <summary>The longest a comment body may be. Sized to the ticket description.</summary>
    public const int BodyMaxLength = Ticket.DescriptionMaxLength;

    /// <summary>The longest a cached author name may be.</summary>
    public const int AuthorNameMaxLength = Ticket.DisplayNameMaxLength;

    private TicketComment()
    {
        // EF Core materialisation; both are non-null in the database.
        Body = null!;
        AuthorName = null!;
    }

    /// <summary>The comment's id. Version 7, so the primary key is time-ordered like the rows.</summary>
    public Guid Id { get; private set; }

    /// <summary>The ticket this belongs to. A real intra-module foreign key.</summary>
    public Guid TicketId { get; private set; }

    /// <summary>What was said.</summary>
    public string Body { get; private set; }

    /// <summary>
    /// True when only a Technician or an Admin may read this. SPEC.md §14 gives a User
    /// "no internal notes", and this flag is the whole of that distinction.
    /// </summary>
    public bool IsInternal { get; private set; }

    /// <summary>Who wrote it. Never null: a comment is always posted inside a request.</summary>
    public Guid AuthorId { get; private set; }

    /// <summary>Their display name at the time. Cached, per the class remarks.</summary>
    public string AuthorName { get; private set; }

    /// <summary>When it was posted (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Posts a comment. The only way one comes into existence.</summary>
    /// <param name="ticketId">The ticket being commented on.</param>
    /// <param name="body">What is being said. Trimmed; must not be blank.</param>
    /// <param name="isInternal">Whether the requester is excluded from reading it.</param>
    /// <param name="authorId">Who is saying it.</param>
    /// <param name="authorName">Their display name, cached onto the row.</param>
    /// <param name="postedAt">When (UTC), from <c>IClock</c>.</param>
    /// <returns>The new comment, not yet persisted.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="ticketId"/> or <paramref name="authorId"/> is empty, or
    /// <paramref name="body"/> is blank.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="body"/> is longer than <see cref="BodyMaxLength"/>.</exception>
    public static TicketComment Post(
        Guid ticketId,
        string body,
        bool isInternal,
        Guid authorId,
        string authorName,
        DateTimeOffset postedAt)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A comment must belong to a ticket.", nameof(ticketId));
        }

        if (authorId == Guid.Empty)
        {
            throw new ArgumentException("A comment must have an author.", nameof(authorId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        var trimmed = body.Trim();

        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmed.Length, BodyMaxLength, nameof(body));

        return new TicketComment
        {
            Id = Guid.CreateVersion7(),
            TicketId = ticketId,
            Body = trimmed,
            IsInternal = isInternal,
            AuthorId = authorId,
            // Truncated rather than refused, following the history entry: an over-long
            // cached name must never be able to stop the thing it describes being recorded.
            AuthorName = Truncate(authorName, AuthorNameMaxLength),
            CreatedAt = postedAt,
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return value.Length > maxLength ? value[..maxLength] : value;
    }
}
