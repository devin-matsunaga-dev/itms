namespace Itms.Modules.Helpdesk.Features.TicketCategories.UpdateTicketCategory;

/// <summary>The body of <c>PUT /api/v1/ticket-categories/{id}</c>.</summary>
/// <remarks>
/// A full replacement rather than a patch: every field is sent, and a null description
/// clears it. Activation is not here — retiring a category is its own action, not a
/// checkbox on an edit form, so it cannot be flipped by accident.
/// </remarks>
/// <param name="Name">The display name. Every ticket already filed under this category follows the rename.</param>
/// <param name="Description">What belongs in it, or <see langword="null"/> to clear it.</param>
/// <param name="SortOrder">Where it sits in a picker.</param>
public sealed record UpdateTicketCategoryRequest(string Name, string? Description, int SortOrder);
