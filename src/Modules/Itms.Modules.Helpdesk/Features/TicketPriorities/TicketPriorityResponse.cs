using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.TicketPriorities;

/// <summary>A ticket priority as the API renders it.</summary>
/// <param name="Id">The priority's id. What a ticket stores.</param>
/// <param name="Code">The stable machine identifier — <c>critical</c>, <c>high</c>. Fixed for the life of the row.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Description">What it is for, or <see langword="null"/>.</param>
/// <param name="Rank">Urgency order, lowest first.</param>
/// <param name="ResponseTargetMinutes">Minutes from creation within which a technician should respond.</param>
/// <param name="ResolutionTargetMinutes">Minutes from creation within which the ticket should be resolved.</param>
/// <param name="IsActive">False once retired.</param>
/// <param name="CreatedAt">When it was created (UTC).</param>
/// <param name="UpdatedAt">When it was last changed (UTC).</param>
public sealed record TicketPriorityResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int Rank,
    int ResponseTargetMinutes,
    int ResolutionTargetMinutes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>The projection every priority query uses, so one shape is built in one place.</summary>
    internal static System.Linq.Expressions.Expression<Func<TicketPriority, TicketPriorityResponse>> Projection() =>
        priority => new TicketPriorityResponse(
            priority.Id,
            priority.Code,
            priority.Name,
            priority.Description,
            priority.Rank,
            priority.ResponseTargetMinutes,
            priority.ResolutionTargetMinutes,
            priority.IsActive,
            priority.CreatedAt,
            priority.UpdatedAt);

    /// <summary>Renders an entity the handler already has in memory.</summary>
    /// <param name="priority">The priority to render.</param>
    /// <returns>The response shape.</returns>
    internal static TicketPriorityResponse From(TicketPriority priority) =>
        new(
            priority.Id,
            priority.Code,
            priority.Name,
            priority.Description,
            priority.Rank,
            priority.ResponseTargetMinutes,
            priority.ResolutionTargetMinutes,
            priority.IsActive,
            priority.CreatedAt,
            priority.UpdatedAt);
}
