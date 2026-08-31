using FluentValidation;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.TicketCategories.UpdateTicketCategory;

/// <summary>Checks the shape of an update request before the handler runs.</summary>
public sealed class UpdateTicketCategoryValidator : AbstractValidator<UpdateTicketCategoryRequest>
{
    /// <summary>Builds the rules.</summary>
    public UpdateTicketCategoryValidator()
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
