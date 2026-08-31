using FluentValidation;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.TicketPriorities.CreateTicketPriority;

/// <summary>Checks the shape of a create request before the handler runs.</summary>
/// <remarks>
/// Uniqueness is not checked here: it needs the database, and a validator that queried
/// one would still lose the race to the unique indexes. The handler owns that, and
/// returns a 409 rather than a 400.
/// </remarks>
public sealed class CreateTicketPriorityValidator : AbstractValidator<CreateTicketPriorityRequest>
{
    /// <summary>Builds the rules.</summary>
    public CreateTicketPriorityValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty().WithMessage("Enter a code.")
            .MaximumLength(PriorityCode.MaxLength)
            .Must(PriorityCode.IsWellFormed).WithMessage(PriorityCode.Requirement);

        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Enter a priority name.")
            .MaximumLength(TicketPriority.NameMaxLength);

        RuleFor(request => request.Description)
            .MaximumLength(TicketPriority.DescriptionMaxLength);

        TicketPriorityTargetRules.Apply(this, r => r.ResponseTargetMinutes, r => r.ResolutionTargetMinutes);
    }
}
