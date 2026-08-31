using FluentValidation;

namespace Itms.Modules.Helpdesk.Features.Tickets.AssignTicket;

/// <summary>Checks the shape of an assignment request before the handler runs.</summary>
/// <remarks>
/// There is almost nothing this can decide. Whether the account exists, is active, and is
/// somebody a ticket may be given to are all facts held in another module; whether the
/// ticket can be assigned at all depends on the state it is in. Both are answered where
/// they are known — the handler through <c>IUserLookup</c>, the entity through
/// <see cref="Domain.Ticket.Assign"/> — so all that is left here is the one thing the
/// request can be wrong about on its own.
/// </remarks>
public sealed class AssignTicketValidator : AbstractValidator<AssignTicketRequest>
{
    /// <summary>Builds the rules.</summary>
    public AssignTicketValidator() =>
        // An omitted assigneeId means "unassign" and is the null. An explicitly empty
        // Guid means the client built the request wrong, and reading it as an
        // unassignment would carry out an instruction nobody gave.
        RuleFor(request => request.AssigneeId)
            .NotEqual(Guid.Empty)
            .WithMessage("Choose a technician to assign the ticket to, or omit the field to unassign it.");
}
