namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// A request for support: the backbone record of the Helpdesk module and the thing
/// SPEC.md §2's whole workflow moves.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this package owns.</b> WP-1.2 owns the ticket's shape, its invariants at
/// creation, and its number. It deliberately owns no behaviour beyond creation: there is
/// no <c>Assign</c>, no <c>ChangeStatus</c>, no <c>Resolve</c>, and no <c>Close</c> here.
/// Those are WP-1.3's state machine and land on this entity, which is why every field
/// they will move already exists and is private to set.
/// </para>
/// <para>
/// <b>Why the forward fields are here and empty.</b> <see cref="AssigneeId"/>,
/// <see cref="DueAt"/>, <see cref="RelatedAssetId"/>, <see cref="RelatedAlertId"/>,
/// <see cref="ResolutionNotes"/>, <see cref="ResolvedAt"/>, <see cref="ClosedAt"/>, and
/// <see cref="DeletedAt"/> are the rest of SPEC.md §2's field set. Their meaning is
/// already fixed by the spec, so declaring them now costs nothing and lets WP-1.3 through
/// WP-3.7 add a method rather than a migration each. None of them has a setter yet: the
/// package that owns the behaviour writes the method that moves the field.
/// </para>
/// <para>
/// <b>Cached display names.</b> <see cref="RequesterName"/>, <see cref="DepartmentName"/>,
/// and <see cref="AssigneeName"/> are copies, held because §3 rule 6 forbids a foreign key
/// across a module boundary and because a fifty-thousand-row queue cannot resolve a name
/// per row. Nothing refreshes them yet — neither Identity nor Directory publishes a
/// rename event — so a renamed department goes stale on tickets already filed. That gap
/// is recorded in STATUS.md; the package that adds those events adds the refresh.
/// </para>
/// <para>
/// <b>Comments, notes, attachments, history, and KB links</b> are tables of their own in
/// WP-1.4, WP-1.7, and WP-4.1. They are not fields on this row.
/// </para>
/// </remarks>
public sealed class Ticket
{
    /// <summary>The longest a subject may be.</summary>
    public const int SubjectMaxLength = 200;

    /// <summary>The longest a description may be.</summary>
    public const int DescriptionMaxLength = 8000;

    /// <summary>The longest the resolution notes may be.</summary>
    public const int ResolutionNotesMaxLength = 8000;

    /// <summary>The longest a cached display name may be. Wide enough for anything Identity or Directory holds.</summary>
    public const int DisplayNameMaxLength = 256;

    private Ticket()
    {
        // EF Core materialisation; all five are non-null in the database.
        Number = null!;
        Subject = null!;
        Description = null!;
        RequesterName = null!;
        DepartmentName = null!;
    }

    /// <summary>The ticket's id. What every other record references.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// The human-readable number, <c>TKT-####</c>. Unique, and immutable once issued:
    /// people quote it in mail and on the phone, so there is no method that changes it.
    /// </summary>
    public string Number { get; private set; }

    /// <summary>The one-line summary.</summary>
    public string Subject { get; private set; }

    /// <summary>What the requester reported.</summary>
    public string Description { get; private set; }

    /// <summary>Who the ticket is for. A user in Identity, referenced by id only (§3 rule 6).</summary>
    public Guid RequesterId { get; private set; }

    /// <summary>The requester's display name, cached at creation. See the class remarks on staleness.</summary>
    public string RequesterName { get; private set; }

    /// <summary>The department the ticket is filed against. A row in Directory, by id only.</summary>
    public Guid DepartmentId { get; private set; }

    /// <summary>That department's name, cached at creation.</summary>
    public string DepartmentName { get; private set; }

    /// <summary>What the ticket is about. A real foreign key: the category is Helpdesk's own row.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>How urgent it is. A real foreign key, for the same reason.</summary>
    public Guid PriorityId { get; private set; }

    /// <summary>Where it sits in the workflow. Always set; <see cref="TicketStatus.New"/> at creation.</summary>
    public TicketStatus Status { get; private set; }

    /// <summary>The technician responsible, or <see langword="null"/> while unassigned. WP-1.6 moves it.</summary>
    public Guid? AssigneeId { get; private set; }

    /// <summary>That technician's display name, cached when they were assigned. WP-1.6 sets it.</summary>
    public string? AssigneeName { get; private set; }

    /// <summary>
    /// When resolution is due under the priority's target, or <see langword="null"/>
    /// until WP-1.8 computes it. That package owns every clock question, this one holds
    /// the column.
    /// </summary>
    public DateTimeOffset? DueAt { get; private set; }

    /// <summary>The asset the ticket concerns, or <see langword="null"/>. WP-2.5 links it.</summary>
    public Guid? RelatedAssetId { get; private set; }

    /// <summary>The alert the ticket was raised from, or <see langword="null"/>. WP-3.7 links it, permanently (invariant 8).</summary>
    public Guid? RelatedAlertId { get; private set; }

    /// <summary>What was done about it, recorded at resolution. WP-1.3 writes it.</summary>
    public string? ResolutionNotes { get; private set; }

    /// <summary>When it was resolved (UTC), or <see langword="null"/>.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>When it was closed (UTC), or <see langword="null"/>. Closed is terminal.</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>When the row was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Who created it, or <see langword="null"/> when the system did.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>When the row was last changed (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Who last changed it, or <see langword="null"/> when the system did.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>
    /// When the ticket was soft-deleted (UTC), or <see langword="null"/> for a live one.
    /// ARCHITECTURE.md §4 makes ticket deletes soft; invariant 9 is why deactivating a
    /// user must never reach this field.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>
    /// Raises a ticket.
    /// </summary>
    /// <remarks>
    /// The status is not a parameter. Invariant 1 requires one and SPEC.md §2's workflow
    /// starts at <see cref="TicketStatus.New"/> for every ticket, so letting a caller
    /// name a starting state would be the first way around WP-1.3's state machine.
    /// </remarks>
    /// <param name="number">The number already issued by <c>TicketNumberGenerator</c>.</param>
    /// <param name="ticket">The fields invariant 1 requires.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is raising it, or <see langword="null"/> for the system.</param>
    /// <returns>The new ticket, not yet persisted.</returns>
    /// <exception cref="ArgumentException">A required field is blank, over-long, or an empty id.</exception>
    public static Ticket Create(string number, NewTicket ticket, DateTimeOffset now, Guid? actor)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        if (!TicketNumber.IsWellFormed(number))
        {
            throw new ArgumentException(
                $"A ticket number must look like {TicketNumber.Format(TicketNumber.FirstValue)}.",
                nameof(number));
        }

        return new Ticket
        {
            // v7 so the primary key is time-ordered and its index does not fragment.
            Id = Guid.CreateVersion7(),
            Number = number,
            Subject = ReferenceText.Name(ticket.Subject, SubjectMaxLength, nameof(ticket)),
            Description = ReferenceText.Name(ticket.Description, DescriptionMaxLength, nameof(ticket)),
            RequesterId = Required(ticket.RequesterId, nameof(ticket.RequesterId), nameof(ticket)),
            RequesterName = ReferenceText.Name(ticket.RequesterName, DisplayNameMaxLength, nameof(ticket)),
            DepartmentId = Required(ticket.DepartmentId, nameof(ticket.DepartmentId), nameof(ticket)),
            DepartmentName = ReferenceText.Name(ticket.DepartmentName, DisplayNameMaxLength, nameof(ticket)),
            CategoryId = Required(ticket.CategoryId, nameof(ticket.CategoryId), nameof(ticket)),
            PriorityId = Required(ticket.PriorityId, nameof(ticket.PriorityId), nameof(ticket)),
            Status = TicketStatus.New,
            CreatedAt = now,
            CreatedBy = actor,
            UpdatedAt = now,
            UpdatedBy = actor,
        };
    }

    private static Guid Required(Guid value, string field, string parameterName) =>
        value == Guid.Empty
            ? throw new ArgumentException($"A ticket requires a {field}.", parameterName)
            : value;
}
