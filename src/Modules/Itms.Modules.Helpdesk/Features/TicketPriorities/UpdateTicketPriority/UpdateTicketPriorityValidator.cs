using FluentValidation;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.TicketPriorities.UpdateTicketPriority;

/// <summary>Checks the shape of an update request before the handler runs.</summary>
public sealed class UpdateTicketPriorityValidator : AbstractValidator<UpdateTicketPriorityRequest>
{
    /// <summary>Builds the rules.</summary>
    public UpdateTicketPriorityValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Enter a priority name.")
            .MaximumLength(TicketPriority.NameMaxLength);

        RuleFor(request => request.Description)
            .MaximumLength(TicketPriority.DescriptionMaxLength);

        TicketPriorityTargetRules.Apply(this, r => r.ResponseTargetMinutes, r => r.ResolutionTargetMinutes);
    }
}
