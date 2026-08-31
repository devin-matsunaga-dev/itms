namespace Itms.Modules.Helpdesk.Features.TicketPriorities.UpdateTicketPriority;

/// <summary>The body of <c>PUT /api/v1/ticket-priorities/{id}</c>.</summary>
/// <remarks>
/// <para>
/// A full replacement rather than a patch: every field is sent, and a null description
/// clears it. Activation is not here — retiring a priority is its own action, not a
/// checkbox on an edit form.
/// </para>
/// <para>
/// <b>The code is not here either, and that is the point.</b> It is immutable once the
/// row exists: a colour map, an integration, or a later rule keyed on <c>critical</c>
/// would silently stop matching if an edit form could change it. Renaming is what an
/// administrator wants; recoding is not.
/// </para>
/// </remarks>
/// <param name="Name">The display name.</param>
/// <param name="Description">What this priority is for, or <see langword="null"/> to clear it.</param>
/// <param name="Rank">Urgency order, lowest first.</param>
/// <param name="ResponseTargetMinutes">Minutes from creation within which a technician should respond.</param>
/// <param name="ResolutionTargetMinutes">Minutes from creation within which the ticket should be resolved.</param>
public sealed record UpdateTicketPriorityRequest(
    string Name,
    string? Description,
    int Rank,
    int ResponseTargetMinutes,
    int ResolutionTargetMinutes);
