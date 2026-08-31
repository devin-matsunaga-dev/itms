using FluentValidation;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.TicketCategories.CreateTicketCategory;

/// <summary>Checks the shape of a create request before the handler runs.</summary>
/// <remarks>
/// Uniqueness is not checked here: it needs the database, and a validator that queried
/// one would still lose the race to the unique index. The handler owns that, and returns
/// a 409 rather than a 400.
/// </remarks>
public sealed class CreateTicketCategoryValidator : AbstractValidator<CreateTicketCategoryRequest>
{
    /// <summary>Builds the rules.</summary>
    public CreateTicketCategoryValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Enter a category name.")
            .MaximumLength(TicketCategory.NameMaxLength);

        RuleFor(request => request.Description)
            .MaximumLength(TicketCategory.DescriptionMaxLength);

        RuleFor(request => request.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("A sort order cannot be negative.");
    }
}
