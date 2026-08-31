using Itms.Platform.Results;

namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// A request for support: the backbone record of the Helpdesk module and the thing
/// SPEC.md §2's whole workflow moves.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shape and number</b> are WP-1.2's. <b>Movement</b> is WP-1.3's: <see cref="ChangeStatus"/>
/// and the intent-named methods around it are the only way <see cref="Status"/> ever
/// changes, and they are the only place <see cref="ResolvedAt"/> and <see cref="ClosedAt"/>
/// are written. <b>Who holds it</b> is WP-1.6's: <see cref="Assign"/> and
/// <see cref="Unassign"/> are the only way <see cref="AssigneeId"/> ever changes, and each
/// moves the status in the same call rather than leaving the two to be set separately.
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

    /// <summary>
    /// The technician responsible, or <see langword="null"/> while unassigned.
    /// </summary>
    /// <remarks>
    /// <b>Non-null exactly when the ticket has left <see cref="TicketStatus.New"/> without
    /// being cancelled.</b> Nothing but <see cref="Assign"/> and <see cref="Unassign"/>
    /// writes it, and both move the status in the same call, so "Assigned to nobody"
    /// cannot be reached: assigning a New ticket moves it to
    /// <see cref="TicketStatus.Assigned"/>, and unassigning an Assigned one moves it back
    /// to New.
    /// </remarks>
    public Guid? AssigneeId { get; private set; }

    /// <summary>That technician's display name, cached when they were assigned. See the class remarks on staleness.</summary>
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

    /// <summary>
    /// What was done about it, recorded at resolution. Required and non-blank whenever
    /// the ticket is resolved; kept, not cleared, when it is reopened, because the notes
    /// are what the requester rejected and the next technician has to read them.
    /// </summary>
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

    /// <summary>
    /// Moves the ticket to <paramref name="target"/> if SPEC.md §2 allows it, and applies
    /// whatever else that move writes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the only guard, and it is inside the entity.</b> Invariant 2 says an
    /// illegal transition is rejected server-side; the endpoint and the client both get
    /// their answer from here, so there is no second copy of the table to drift out of
    /// step with <see cref="TicketStateMachine"/>. The intent-named wrappers below —
    /// <see cref="Start"/>, <see cref="Wait"/>, <see cref="Resume"/>,
    /// <see cref="Resolve"/>, <see cref="Reopen"/>, <see cref="Close"/>,
    /// <see cref="Cancel"/> — all land here.
    /// </para>
    /// <para>
    /// A failure returns rather than throws: an illegal transition is something a caller
    /// can act on, which CONVENTIONS.md says travels as a <see cref="Result"/>. Nothing
    /// on the entity is written on a failure.
    /// </para>
    /// <para>
    /// <b>Two destinations are not this method's to give.</b> Both
    /// <see cref="TicketStatus.Assigned"/> and <see cref="TicketStatus.New"/> are legal in
    /// the table, and both are half of a change this method cannot make: reaching Assigned
    /// means naming somebody, and returning to New means clearing them. A ticket that was
    /// Assigned to nobody, or New while somebody still held it, would be a row that
    /// contradicts its own status. <see cref="Assign"/> and <see cref="Unassign"/> compose
    /// the two writes; this method refuses <see cref="TicketStatus.New"/> outright, and
    /// the status-change endpoint refuses <see cref="TicketStatus.Assigned"/> because it
    /// carries no assignee to go with it.
    /// </para>
    /// </remarks>
    /// <param name="target">The status being asked for.</param>
    /// <param name="resolutionNotes">
    /// What was done, required and non-blank when <paramref name="target"/> is
    /// <see cref="TicketStatus.Resolved"/> and rejected otherwise — no other transition
    /// records a resolution.
    /// </param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is making the move.</param>
    /// <returns>Success, or a conflict describing why the move is refused.</returns>
    public Result ChangeStatus(
        TicketStatus target,
        string? resolutionNotes,
        DateTimeOffset now,
        Guid? actor)
    {
        if (target == TicketStatus.New && TicketStateMachine.CanTransition(Status, TicketStatus.New))
        {
            // The table allows Assigned → New, because unassignment is a real transition
            // and has to be refused from the wrong state like any other. Only Unassign may
            // walk it: it clears the assignee in the same call, and a bare move here would
            // leave a New ticket still holding a technician.
            //
            // Guarded on the edge existing, not on the destination, so that every other
            // request for New — from Closed, from Waiting, from New itself — keeps the
            // plain illegal_transition it has always answered with. Only the one state
            // that could walk the edge is told to use the door instead.
            return HelpdeskErrors.UnassignToReturnToNew();
        }

        return Move(target, resolutionNotes, now, actor);
    }

    /// <summary>
    /// The transition itself, with no opinion about which destinations a caller is
    /// entitled to ask for.
    /// </summary>
    /// <remarks>
    /// Private, and the only writer of <see cref="Status"/>. <see cref="ChangeStatus"/>
    /// is the door for every destination but <see cref="TicketStatus.New"/>;
    /// <see cref="Unassign"/> is the door for that one, and it is a door precisely
    /// because it also clears the assignee.
    /// </remarks>
    /// <param name="target">The status being moved to.</param>
    /// <param name="resolutionNotes">The resolution, when resolving.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is making the move.</param>
    /// <returns>Success, or a conflict describing why the move is refused.</returns>
    private Result Move(
        TicketStatus target,
        string? resolutionNotes,
        DateTimeOffset now,
        Guid? actor)
    {
        if (!TicketStateMachine.CanTransition(Status, target))
        {
            return HelpdeskErrors.IllegalTransition(Status, target);
        }

        if (target == TicketStatus.Resolved)
        {
            if (string.IsNullOrWhiteSpace(resolutionNotes))
            {
                return HelpdeskErrors.ResolutionNotesRequired();
            }

            if (resolutionNotes.Length > ResolutionNotesMaxLength)
            {
                return HelpdeskErrors.ResolutionNotesTooLong();
            }
        }
        else if (resolutionNotes is not null)
        {
            return HelpdeskErrors.ResolutionNotesNotAccepted(target);
        }

        switch (target)
        {
            case TicketStatus.InProgress:
                // Reopening: the ticket is not resolved any more, so the instant goes. The
                // notes stay — see the remarks on ResolutionNotes.
                if (Status == TicketStatus.Resolved)
                {
                    ResolvedAt = null;
                }

                break;

            case TicketStatus.Resolved:
                ResolutionNotes = resolutionNotes!.Trim();
                ResolvedAt = now;
                break;

            case TicketStatus.Closed:
                ClosedAt = now;
                break;

            case TicketStatus.Waiting:
            case TicketStatus.Cancelled:
                // Neither writes a field of its own. Waiting's effect on the SLA clock is
                // WP-1.8's, computed from the history rather than stored here, and there
                // is no CancelledAt column: UpdatedAt and WP-1.4's history say when.
                break;

            case TicketStatus.Assigned:
                // Reached only through Assign, which sets the assignee in the same breath.
                // See the remarks: this method will not stop a caller asking for it bare,
                // and the status-change endpoint is what refuses to.
                break;

            case TicketStatus.New:
                // Reached only through Unassign, which clears the assignee in the same
                // breath. ChangeStatus refuses this destination before it ever gets here.
                break;

            default:
                // Unreachable for a defined TicketStatus; a value outside the enum would
                // already have failed CanTransition above.
                return HelpdeskErrors.IllegalTransition(Status, target);
        }

        Status = target;
        UpdatedAt = now;
        UpdatedBy = actor;

        return Result.Success();
    }

    /// <summary>A technician started work. <see cref="TicketStatus.Assigned"/> → <see cref="TicketStatus.InProgress"/>.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who started.</param>
    /// <returns>Success, or a conflict.</returns>
    public Result Start(DateTimeOffset now, Guid? actor) =>
        ChangeStatus(TicketStatus.InProgress, resolutionNotes: null, now, actor);

    /// <summary>Work is blocked on somebody else. → <see cref="TicketStatus.Waiting"/>.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who parked it.</param>
    /// <returns>Success, or a conflict.</returns>
    public Result Wait(DateTimeOffset now, Guid? actor) =>
        ChangeStatus(TicketStatus.Waiting, resolutionNotes: null, now, actor);

    /// <summary>Whatever it was waiting on arrived. <see cref="TicketStatus.Waiting"/> → <see cref="TicketStatus.InProgress"/>.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who resumed it.</param>
    /// <returns>Success, or a conflict.</returns>
    public Result Resume(DateTimeOffset now, Guid? actor) =>
        ChangeStatus(TicketStatus.InProgress, resolutionNotes: null, now, actor);

    /// <summary>The problem is fixed, pending the requester's acceptance. → <see cref="TicketStatus.Resolved"/>.</summary>
    /// <param name="resolutionNotes">What was done. Required and non-blank.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who resolved it.</param>
    /// <returns>Success, or a conflict.</returns>
    public Result Resolve(string resolutionNotes, DateTimeOffset now, Guid? actor) =>
        ChangeStatus(TicketStatus.Resolved, resolutionNotes, now, actor);

    /// <summary>The fix did not hold. <see cref="TicketStatus.Resolved"/> → <see cref="TicketStatus.InProgress"/>.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who reopened it.</param>
    /// <returns>Success, or a conflict.</returns>
    public Result Reopen(DateTimeOffset now, Guid? actor) =>
        ChangeStatus(TicketStatus.InProgress, resolutionNotes: null, now, actor);

    /// <summary>The requester accepted the resolution. <see cref="TicketStatus.Resolved"/> → <see cref="TicketStatus.Closed"/>, which is terminal.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who closed it.</param>
    /// <returns>Success, or a conflict.</returns>
    public Result Close(DateTimeOffset now, Guid? actor) =>
        ChangeStatus(TicketStatus.Closed, resolutionNotes: null, now, actor);

    /// <summary>Abandoned before resolution. Legal from any pre-resolved state, and terminal.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who cancelled it.</param>
    /// <returns>Success, or a conflict.</returns>
    public Result Cancel(DateTimeOffset now, Guid? actor) =>
        ChangeStatus(TicketStatus.Cancelled, resolutionNotes: null, now, actor);

    /// <summary>
    /// Puts <paramref name="assigneeId"/> in charge of the ticket, moving it out of
    /// <see cref="TicketStatus.New"/> if that is where it was.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The assignee and the status are written in one call, and that is the point.</b>
    /// A ticket in <see cref="TicketStatus.Assigned"/> that nobody holds is a row that
    /// contradicts its own status, and the only way to be sure it never exists is for the
    /// entity — not a handler that remembered both steps — to make them together.
    /// </para>
    /// <para>
    /// <b>Reassignment preserves the status.</b> Handing an In Progress ticket to somebody
    /// else does not restart it, and handing on a Waiting one does not unblock it; only
    /// the first assignment moves the workflow, because only New has somewhere to move to.
    /// </para>
    /// <para>
    /// <b>Whether the assignee is a technician is not decided here.</b> That is a fact
    /// about an account in another module, which this entity cannot read and must not
    /// guess at; the handler resolves it through <c>IUserLookup</c> before calling in.
    /// What is decided here is that a terminal ticket has no work left to hand anybody.
    /// </para>
    /// </remarks>
    /// <param name="assigneeId">The technician taking it on.</param>
    /// <param name="assigneeName">Their display name, cached on the row (§3 rule 6).</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is making the assignment.</param>
    /// <returns>Success, or a conflict describing why the assignment is refused.</returns>
    /// <exception cref="ArgumentException">The id is empty or the name is blank or over-long.</exception>
    public Result Assign(Guid assigneeId, string assigneeName, DateTimeOffset now, Guid? actor)
    {
        var id = Required(assigneeId, nameof(assigneeId), nameof(assigneeId));
        var name = ReferenceText.Name(assigneeName, DisplayNameMaxLength, nameof(assigneeName));

        if (TicketStateMachine.IsTerminal(Status))
        {
            return HelpdeskErrors.TicketNotAssignable(Status);
        }

        if (id == AssigneeId)
        {
            // Not a no-op: it would write a history line saying the ticket moved from a
            // technician to the same technician. The same call WP-1.3 made for a status
            // that is already the ticket's own.
            return HelpdeskErrors.AlreadyAssignedToThatTechnician(name);
        }

        // Moved first, so a refused transition leaves the assignee untouched. From any
        // state but New there is nothing to move: only the first assignment starts the
        // workflow.
        if (Status == TicketStatus.New)
        {
            var moved = Move(TicketStatus.Assigned, resolutionNotes: null, now, actor);

            if (moved.IsFailure)
            {
                return moved;
            }
        }

        AssigneeId = id;
        AssigneeName = name;
        UpdatedAt = now;
        UpdatedBy = actor;

        return Result.Success();
    }

    /// <summary>
    /// Takes the ticket back off whoever holds it, returning it to
    /// <see cref="TicketStatus.New"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only an <see cref="TicketStatus.Assigned"/> ticket can be unassigned</b>, and it
    /// goes back to New rather than sitting assigned to nobody. Once work has started —
    /// In Progress, Waiting, Resolved — the ticket has a history somebody owns, and the
    /// answer to "this is not mine" is to hand it on with <see cref="Assign"/>, not to
    /// drop it back on the queue as though nothing had happened.
    /// </para>
    /// <para>
    /// <c>Assigned → New</c> is the one edge SPEC.md §2 does not draw. It is in the table
    /// at the human's direction, as the alternative to a ticket whose status claims an
    /// owner it does not have.
    /// </para>
    /// </remarks>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is unassigning it.</param>
    /// <returns>Success, or a conflict describing why the ticket cannot be unassigned.</returns>
    public Result Unassign(DateTimeOffset now, Guid? actor)
    {
        if (AssigneeId is null)
        {
            return HelpdeskErrors.TicketNotAssigned();
        }

        if (Status != TicketStatus.Assigned)
        {
            return HelpdeskErrors.CannotUnassignFrom(Status);
        }

        var moved = Move(TicketStatus.New, resolutionNotes: null, now, actor);

        if (moved.IsFailure)
        {
            return moved;
        }

        AssigneeId = null;
        AssigneeName = null;
        UpdatedAt = now;
        UpdatedBy = actor;

        return Result.Success();
    }

    private static Guid Required(Guid value, string field, string parameterName) =>
        value == Guid.Empty
            ? throw new ArgumentException($"A ticket requires a {field}.", parameterName)
            : value;
}
