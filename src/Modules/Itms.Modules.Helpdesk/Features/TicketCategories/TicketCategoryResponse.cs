using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.TicketCategories;

/// <summary>A ticket category as the API renders it.</summary>
/// <param name="Id">The category's id. What a ticket stores, and what a rename does not change.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Description">What belongs in it, or <see langword="null"/>.</param>
/// <param name="SortOrder">Where it sits in a picker.</param>
/// <param name="IsActive">False once retired.</param>
/// <param name="CreatedAt">When it was created (UTC).</param>
/// <param name="UpdatedAt">When it was last changed (UTC).</param>
public sealed record TicketCategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>The projection every category query uses, so one shape is built in one place.</summary>
    internal static System.Linq.Expressions.Expression<Func<TicketCategory, TicketCategoryResponse>> Projection() =>
        category => new TicketCategoryResponse(
            category.Id,
            category.Name,
            category.Description,
            category.SortOrder,
            category.IsActive,
            category.CreatedAt,
            category.UpdatedAt);

    /// <summary>Renders an entity the handler already has in memory.</summary>
    /// <param name="category">The category to render.</param>
    /// <returns>The response shape.</returns>
    internal static TicketCategoryResponse From(TicketCategory category) =>
        new(
            category.Id,
            category.Name,
            category.Description,
            category.SortOrder,
            category.IsActive,
            category.CreatedAt,
            category.UpdatedAt);
}
