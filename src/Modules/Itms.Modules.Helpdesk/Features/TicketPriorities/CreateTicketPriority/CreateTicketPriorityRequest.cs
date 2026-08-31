namespace Itms.Modules.Helpdesk.Features.TicketPriorities.CreateTicketPriority;

/// <summary>The body of <c>POST /api/v1/ticket-priorities</c>.</summary>
/// <remarks>
/// The code is settable here and nowhere else. It is the key colours, integrations, and
/// later rules resolve against, and every one of them would break silently if it moved,
/// so creation is the only chance to choose it.
/// </remarks>
/// <param name="Code">The stable machine identifier, lower-cased on the way in. Unique and immutable.</param>
/// <param name="Name">The display name. Unique, case-insensitively, and editable.</param>
/// <param name="Description">What this priority is for, or <see langword="null"/>.</param>
/// <param name="Rank">Urgency order, lowest first. Ties are broken by name.</param>
/// <param name="ResponseTargetMinutes">Minutes from creation within which a technician should respond.</param>
/// <param name="ResolutionTargetMinutes">Minutes from creation within which the ticket should be resolved.</param>
public sealed record CreateTicketPriorityRequest(
    string Code,
    string Name,
    string? Description,
    int Rank,
    int ResponseTargetMinutes,
    int ResolutionTargetMinutes);
