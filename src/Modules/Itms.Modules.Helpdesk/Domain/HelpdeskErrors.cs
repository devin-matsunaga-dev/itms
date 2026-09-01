using Itms.Platform.Results;

namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// Every failure this module can return, written once.
/// </summary>
/// <remarks>
/// The codes are part of the API surface — clients switch on them — so they live in one
/// file where a reword is visible in review rather than being spelled out at each call
/// site that can produce them.
/// </remarks>
internal static class HelpdeskErrors
{
    public static Error CategoryNotFound() =>
        Error.NotFound("helpdesk.category_not_found", "No such ticket category.");

    public static Error DuplicateCategoryName(string name) =>
        Error.Conflict("helpdesk.duplicate_category_name", $"A ticket category named '{name}' already exists.");

    public static Error PriorityNotFound() =>
        Error.NotFound("helpdesk.priority_not_found", "No such ticket priority.");

    public static Error DuplicatePriorityName(string name) =>
        Error.Conflict("helpdesk.duplicate_priority_name", $"A ticket priority named '{name}' already exists.");

    /// <summary>
    /// The code is the key everything other than a person reads, so a second row claiming
    /// one is refused rather than disambiguated.
    /// </summary>
    public static Error DuplicatePriorityCode(string code) =>
        Error.Conflict("helpdesk.duplicate_priority_code", $"A ticket priority with the code '{code}' already exists.");

    /// <summary>
    /// The ticket does not exist, has been soft-deleted, or belongs to somebody else.
    /// </summary>
    /// <remarks>
    /// The third case is deliberate and is the one exception ARCHITECTURE.md §6 allows to
    /// "forbidden is 403 and never a 404 disguise": a User asking after a ticket they did
    /// not raise gets the same answer as one asking after a ticket that was never issued,
    /// because telling them apart would let any account walk the id space and count what
    /// it cannot see. <see cref="Features.Tickets.TicketScope"/> is what makes the two
    /// indistinguishable, by never returning the row in the first place.
    /// </remarks>
    public static Error TicketNotFound() =>
        Error.NotFound("helpdesk.ticket_not_found", "No such ticket.");

    /// <summary>The category exists but has been retired, so no new ticket may be filed against it.</summary>
    /// <remarks>
    /// Retired rather than deleted is WP-1.1's call — existing tickets keep pointing at it
    /// and keep rendering its name. Only creation is refused.
    /// </remarks>
    public static Error CategoryRetired() =>
        Error.Validation(
            "helpdesk.category_retired",
            "That ticket category has been retired. Choose another.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["categoryId"] = ["That ticket category has been retired. Choose another."],
            });

    /// <summary>The priority exists but has been retired.</summary>
    public static Error PriorityRetired() =>
        Error.Validation(
            "helpdesk.priority_retired",
            "That ticket priority has been retired. Choose another.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["priorityId"] = ["That ticket priority has been retired. Choose another."],
            });

    /// <summary>No such user, as far as Identity is concerned.</summary>
    public static Error RequesterNotFound() =>
        Error.Validation(
            "helpdesk.requester_not_found",
            "No such user.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["requesterId"] = ["No such user."],
            });

    /// <summary>The requester's account has been deactivated.</summary>
    /// <remarks>
    /// Invariant 9 keeps a deactivated person's existing tickets; it does not say new ones
    /// may be raised for them, and a ticket nobody can be contacted about is not worth
    /// filing.
    /// </remarks>
    public static Error RequesterInactive() =>
        Error.Validation(
            "helpdesk.requester_inactive",
            "That account has been deactivated, so a ticket cannot be raised for it.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["requesterId"] = ["That account has been deactivated, so a ticket cannot be raised for it."],
            });

    /// <summary>
    /// A User tried to raise a ticket naming somebody else as the requester.
    /// </summary>
    /// <remarks>
    /// A 403 rather than quietly substituting the caller's own id, at the human's
    /// direction: silent coercion hides both a client bug and an attempt to file under
    /// another name, and neither should look like success.
    /// </remarks>
    public static Error RequesterNotSelf() =>
        Error.Forbidden(
            "helpdesk.requester_not_self",
            "You can only raise a ticket for yourself.");

    /// <summary>No such department, as far as Directory is concerned.</summary>
    public static Error DepartmentNotFound() =>
        Error.Validation(
            "helpdesk.department_not_found",
            "No such department.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["departmentId"] = ["No such department."],
            });

    /// <summary>The department exists but has been retired.</summary>
    public static Error DepartmentRetired() =>
        Error.Validation(
            "helpdesk.department_retired",
            "That department has been retired. Choose another.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["departmentId"] = ["That department has been retired. Choose another."],
            });

    /// <summary>
    /// No department was given and the requester has none on their account to fall back
    /// to.
    /// </summary>
    /// <remarks>
    /// Invariant 1 does not name the department, but the column is <c>NOT NULL</c> and
    /// SPEC.md §2 lists it among a ticket's fields, so a ticket has to arrive with one.
    /// The fallback exists so an end user filing their own ticket does not have to answer
    /// a question their account already answers.
    /// </remarks>
    public static Error DepartmentRequired() =>
        Error.Validation(
            "helpdesk.department_required",
            "Choose a department: the requester's account does not name one.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["departmentId"] = ["Choose a department: the requester's account does not name one."],
            });

    /// <summary>
    /// The caller's <c>If-Match</c> names a version the ticket has moved past.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A 412, not a 409, and the difference is that <b>nothing was attempted</b>: the
    /// precondition was checked before the transition, so the caller can reload and retype
    /// nothing. <see cref="TicketChangedConcurrently"/> is the other one — the write was
    /// attempted and lost a race.
    /// </para>
    /// <para>
    /// It carries the same code as that conflict on purpose. Both mean "your copy of this
    /// ticket is stale, reload it", which is the one thing a client does about either; the
    /// status is what separates them for anybody who cares which happened.
    /// </para>
    /// </remarks>
    public static Error TicketPreconditionFailed() =>
        Error.PreconditionFailed(
            "helpdesk.ticket_conflict",
            "The ticket has changed since you loaded it. Reload it and try again.");

    /// <summary>
    /// The move is not one SPEC.md §2 allows. A 409 rather than a 400: the request was
    /// well formed and the transition exists in general — it is this ticket's current
    /// state that refuses it, which is what <see cref="ErrorKind.Conflict"/> means.
    /// </summary>
    public static Error IllegalTransition(TicketStatus from, TicketStatus to) =>
        Error.Conflict(
            "helpdesk.illegal_transition",
            TicketStateMachine.IsTerminal(from)
                ? $"A {Describe(from)} ticket cannot change status."
                : $"A ticket cannot move from {Describe(from)} to {Describe(to)}.");

    /// <summary>Resolving without saying what was done leaves the work undocumented.</summary>
    public static Error ResolutionNotesRequired() =>
        Error.Validation(
            "helpdesk.resolution_notes_required",
            "Describe what was done to resolve the ticket.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["resolutionNotes"] = ["Describe what was done to resolve the ticket."],
            });

    /// <summary>The notes exceed what the column holds.</summary>
    public static Error ResolutionNotesTooLong() =>
        Error.Validation(
            "helpdesk.resolution_notes_too_long",
            $"Resolution notes cannot be longer than {Ticket.ResolutionNotesMaxLength} characters.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["resolutionNotes"] =
                    [$"Resolution notes cannot be longer than {Ticket.ResolutionNotesMaxLength} characters."],
            });

    /// <summary>
    /// Only resolving records a resolution. Silently dropping the notes on any other
    /// transition would lose text somebody typed.
    /// </summary>
    public static Error ResolutionNotesNotAccepted(TicketStatus target) =>
        Error.Validation(
            "helpdesk.resolution_notes_not_accepted",
            $"Resolution notes are only recorded when resolving a ticket, not when moving it to {Describe(target)}.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["resolutionNotes"] = ["Resolution notes are only recorded when resolving a ticket."],
            });

    /// <summary>Parking a ticket without saying what it is waiting on tells nobody anything.</summary>
    /// <remarks>
    /// The mirror of <see cref="ResolutionNotesRequired"/>. A ticket in Waiting is one
    /// nobody is working, and "why" is the only thing that lets the next person — or the
    /// requester reading their own timeline — know whether it is waiting on them.
    /// </remarks>
    public static Error HoldReasonRequired() =>
        Error.Validation(
            "helpdesk.hold_reason_required",
            "Say what the ticket is waiting on.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["holdReason"] = ["Say what the ticket is waiting on."],
            });

    /// <summary>The reason exceeds what the column holds.</summary>
    public static Error HoldReasonTooLong() =>
        Error.Validation(
            "helpdesk.hold_reason_too_long",
            $"A hold reason cannot be longer than {Ticket.HoldReasonMaxLength} characters.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["holdReason"] = [$"A hold reason cannot be longer than {Ticket.HoldReasonMaxLength} characters."],
            });

    /// <summary>
    /// Only holding a ticket records a reason, exactly as only resolving records a
    /// resolution. Silently dropping text somebody typed is worse than refusing it.
    /// </summary>
    public static Error HoldReasonNotAccepted(TicketStatus target) =>
        Error.Validation(
            "helpdesk.hold_reason_not_accepted",
            $"A hold reason is only recorded when putting a ticket on hold, not when moving it to {Describe(target)}.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["holdReason"] = ["A hold reason is only recorded when putting a ticket on hold."],
            });

    /// <summary>No such user, as far as Identity is concerned.</summary>
    public static Error AssigneeNotFound() =>
        Error.Validation(
            "helpdesk.assignee_not_found",
            "No such user.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["assigneeId"] = ["No such user."],
            });

    /// <summary>The account exists but has been deactivated, so it cannot be given work.</summary>
    public static Error AssigneeInactive() =>
        Error.Validation(
            "helpdesk.assignee_inactive",
            "That account has been deactivated, so a ticket cannot be assigned to it.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["assigneeId"] = ["That account has been deactivated, so a ticket cannot be assigned to it."],
            });

    /// <summary>
    /// The account exists and is active but holds neither the Technician nor the Admin
    /// role, so it is not somebody a ticket can be given to.
    /// </summary>
    /// <remarks>
    /// Checked server-side against <c>IUserLookup</c>'s role list rather than left to the
    /// picker: ARCHITECTURE.md §7 says the React app hiding what a role cannot do is never
    /// the enforcement, and assigning a ticket to an end user would put it in a queue they
    /// have no route to work.
    /// </remarks>
    public static Error AssigneeNotTechnician() =>
        Error.Validation(
            "helpdesk.assignee_not_technician",
            "A ticket can only be assigned to a technician or an administrator.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["assigneeId"] = ["A ticket can only be assigned to a technician or an administrator."],
            });

    /// <summary>The ticket is Closed or Cancelled, so there is no work left to hand anybody.</summary>
    public static Error TicketNotAssignable(TicketStatus status) =>
        Error.Conflict(
            "helpdesk.ticket_not_assignable",
            $"A {Describe(status)} ticket cannot be assigned.");

    /// <summary>
    /// The ticket is already held by the technician the caller named.
    /// </summary>
    /// <remarks>
    /// A conflict rather than a silent success, for the reason WP-1.3 refused a move to
    /// the status a ticket is already in: it would write a history line saying the ticket
    /// passed from somebody to themselves.
    /// </remarks>
    public static Error AlreadyAssignedToThatTechnician(string assigneeName) =>
        Error.Conflict(
            "helpdesk.already_assigned",
            $"The ticket is already assigned to {assigneeName}.");

    /// <summary>Nobody holds the ticket, so there is nothing to take off them.</summary>
    public static Error TicketNotAssigned() =>
        Error.Conflict(
            "helpdesk.ticket_not_assigned",
            "The ticket is not assigned to anybody.");

    /// <summary>
    /// The ticket has moved past <see cref="TicketStatus.Assigned"/>, so it cannot be
    /// dropped back on the queue.
    /// </summary>
    /// <remarks>
    /// Work that has started belongs to somebody until it is handed on. Reassigning is the
    /// answer to "this is not mine"; unassigning would leave an In Progress ticket with no
    /// owner, which is the state this package exists to make unreachable.
    /// </remarks>
    public static Error CannotUnassignFrom(TicketStatus status) =>
        Error.Conflict(
            "helpdesk.cannot_unassign",
            $"A {Describe(status)} ticket cannot be unassigned. Assign it to somebody else instead.");

    /// <summary>
    /// A caller asked the general status mover to return a ticket to
    /// <see cref="TicketStatus.New"/>.
    /// </summary>
    /// <remarks>
    /// <c>Assigned → New</c> is legal in the table, but it is unassignment, and
    /// unassignment clears the assignee in the same call. Walking the edge without doing
    /// that would leave a New ticket still holding a technician — so the mover refuses it
    /// and names the operation that does it properly.
    /// </remarks>
    public static Error UnassignToReturnToNew() =>
        Error.Conflict(
            "helpdesk.unassign_to_return_to_new",
            "Unassign the ticket to return it to New.");

    /// <summary>
    /// A User asked to post an internal note, or to attach a file only the queue can see.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A 403, not a 404 and not a silent downgrade to a public comment. The 404 exception
    /// ARCHITECTURE.md §6 allows is about not letting a caller enumerate ids, and nothing
    /// is being enumerated here — the caller already holds a ticket they may read, and the
    /// only fact this answer reveals is that internal notes exist, which SPEC.md §14 states
    /// in the role table anyway.
    /// </para>
    /// <para>
    /// Silently posting it as a public comment would be worse than either: the author would
    /// believe they had written something the requester cannot see, and the requester would
    /// be reading it.
    /// </para>
    /// </remarks>
    public static Error InternalCommentForbidden() =>
        Error.Forbidden(
            "helpdesk.internal_not_permitted",
            "Only a technician or an administrator can write an internal note.");

    /// <summary>No such attachment on this ticket, or none the caller may see.</summary>
    /// <remarks>
    /// Covers three cases on purpose, exactly as <see cref="TicketNotFound"/> covers three:
    /// no such file, a file on somebody else's ticket, and an internal file the caller is
    /// not inside the queue for. Answering the third differently would let a requester
    /// discover that their technician attached something they cannot see, which is the one
    /// thing the internal flag exists to prevent.
    /// </remarks>
    public static Error AttachmentNotFound() =>
        Error.NotFound("helpdesk.attachment_not_found", "No such attachment.");

    /// <summary>The upload carried no file, or an empty one.</summary>
    public static Error AttachmentFileRequired() =>
        Error.Validation(
            "helpdesk.attachment_file_required",
            "Attach a file.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["file"] = ["Attach a file."],
            });

    /// <summary>The file is bigger than the deployment allows.</summary>
    public static Error AttachmentTooLarge(long maxBytes)
    {
        var megabytes = maxBytes / (1024d * 1024d);
        var message = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"That file is larger than the {megabytes:0.#} MB limit.");

        return Error.Validation(
            "helpdesk.attachment_too_large",
            message,
            new Dictionary<string, string[]>(StringComparer.Ordinal) { ["file"] = [message] });
    }

    /// <summary>The file's extension is not one this deployment accepts.</summary>
    public static Error AttachmentTypeNotAllowed(IEnumerable<string> allowed)
    {
        var message = $"That kind of file is not accepted. Allowed types: {string.Join(", ", allowed)}.";

        return Error.Validation(
            "helpdesk.attachment_type_not_allowed",
            message,
            new Dictionary<string, string[]>(StringComparer.Ordinal) { ["file"] = [message] });
    }

    /// <summary>
    /// The extension is accepted but the bytes are not what that extension describes.
    /// </summary>
    /// <remarks>
    /// The one check that survives a caller renaming a file to get past the allowlist.
    /// CONVENTIONS.md's security floor names content-type sniffing beside the allowlist for
    /// exactly this reason: an allowlist alone is a check on a string the uploader chose.
    /// </remarks>
    public static Error AttachmentContentMismatch()
    {
        const string Message = "The file's contents do not match its extension.";

        return Error.Validation(
            "helpdesk.attachment_content_mismatch",
            Message,
            new Dictionary<string, string[]>(StringComparer.Ordinal) { ["file"] = [Message] });
    }

    /// <summary>
    /// The row is there and the bytes are not. A 500, because the caller did nothing wrong
    /// and there is nothing they can do about it.
    /// </summary>
    /// <remarks>
    /// Reachable by a restore that brought the database back without the storage volume, or
    /// by somebody tidying the directory. It is worth its own code rather than a bare
    /// exception, so the log and the operator see "the file is missing" instead of a stack
    /// trace ending in the filesystem.
    /// </remarks>
    public static Error AttachmentContentMissing() =>
        Error.Unexpected(
            "helpdesk.attachment_unavailable",
            "The attachment's contents could not be read.");

    /// <summary>
    /// Somebody else moved the ticket between this request reading it and writing it.
    /// The <c>xmin</c> token WP-1.2 mapped is what notices.
    /// </summary>
    public static Error TicketChangedConcurrently() =>
        Error.Conflict(
            "helpdesk.ticket_conflict",
            "The ticket was changed by somebody else. Reload it and try again.");

    /// <summary>
    /// The status as a person reads it. The enum names are the wire format, but
    /// <c>InProgress</c> in a sentence shown to a technician reads as a typo.
    /// </summary>
    private static string Describe(TicketStatus status) => status switch
    {
        TicketStatus.InProgress => "In Progress",
        _ => status.ToString(),
    };
}
