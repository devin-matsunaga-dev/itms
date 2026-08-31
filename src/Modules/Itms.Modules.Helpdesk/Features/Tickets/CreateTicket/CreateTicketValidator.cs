using FluentValidation;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.Tickets.CreateTicket;

/// <summary>Checks the shape of a create request before the handler runs.</summary>
/// <remarks>
/// <para>
/// Shape only. Whether the category exists, whether the priority has been retired, whether
/// the requester's account is active — all of those need the database or another module,
/// and all of them are the handler's. A validator that queried for them would still lose
/// the race to the row it read.
/// </para>
/// <para>
/// The lengths come from <see cref="Ticket"/>'s own constants, so the request cannot
/// accept text the column will not hold. <see cref="Ticket.Create"/> throws on the same
/// bounds — this is what turns that programming error into a 400 with the field named.
/// </para>
/// </remarks>
public sealed class CreateTicketValidator : AbstractValidator<CreateTicketRequest>
{
    /// <summary>Builds the rules.</summary>
    public CreateTicketValidator()
    {
        RuleFor(request => request.Subject)
            .NotEmpty().WithMessage("Enter a title for the ticket.")
            .MaximumLength(Ticket.SubjectMaxLength);

        RuleFor(request => request.Description)
            .NotEmpty().WithMessage("Describe what is wrong.")
            .MaximumLength(Ticket.DescriptionMaxLength);

        RuleFor(request => request.CategoryId)
            .NotEmpty().WithMessage("Choose a category.");

        RuleFor(request => request.PriorityId)
            .NotEmpty().WithMessage("Choose a priority.");

        // Optional, but an explicitly empty Guid is somebody's uninitialised field rather
        // than an omission, and it would otherwise reach the lookup as a real id.
        RuleFor(request => request.RequesterId)
            .NotEqual(Guid.Empty).WithMessage("Choose a requester.")
            .When(request => request.RequesterId.HasValue);

        RuleFor(request => request.DepartmentId)
            .NotEqual(Guid.Empty).WithMessage("Choose a department.")
            .When(request => request.DepartmentId.HasValue);
    }
}
