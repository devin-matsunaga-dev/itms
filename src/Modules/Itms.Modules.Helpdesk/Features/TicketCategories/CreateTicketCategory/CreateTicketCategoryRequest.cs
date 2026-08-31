namespace Itms.Modules.Helpdesk.Features.TicketCategories.CreateTicketCategory;

/// <summary>The body of <c>POST /api/v1/ticket-categories</c>.</summary>
/// <param name="Name">The display name. Unique, case-insensitively.</param>
/// <param name="Description">What belongs in this category, or <see langword="null"/>.</param>
/// <param name="SortOrder">Where it sits in a picker. Ties are broken by name.</param>
public sealed record CreateTicketCategoryRequest(string Name, string? Description, int SortOrder);
