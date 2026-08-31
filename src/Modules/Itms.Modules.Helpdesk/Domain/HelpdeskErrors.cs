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

    /// <summary>The ticket does not exist, or has been soft-deleted.</summary>
    public static Error TicketNotFound() =>
        Error.NotFound("helpdesk.ticket_not_found", "No such ticket.");

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
