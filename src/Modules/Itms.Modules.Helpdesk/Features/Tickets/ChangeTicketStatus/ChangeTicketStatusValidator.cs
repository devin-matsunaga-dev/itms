using FluentValidation;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.Tickets.ChangeTicketStatus;

/// <summary>Checks the shape of a status-change request before the handler runs.</summary>
/// <remarks>
/// <para>
/// This checks only what can be known without the ticket: that the destination is a real
/// status, and that the resolution notes are present exactly when they mean something.
/// <b>Whether the move itself is legal is not checked here</b> — that depends on the state
/// the ticket is actually in, so <see cref="TicketStateMachine"/> answers it inside the
/// entity and the caller gets one consistent 409 for every refused move rather than a 400
/// from here for some of them.
/// </para>
/// <para>
/// The entity re-checks both of these rules. That is deliberate on a <c>[SENSITIVE]</c>
/// package: this filter is what produces a good error message, and the entity is what
/// makes the rule true.
/// </para>
/// </remarks>
public sealed class ChangeTicketStatusValidator : AbstractValidator<ChangeTicketStatusRequest>
{
    /// <summary>Builds the rules.</summary>
    public ChangeTicketStatusValidator()
    {
        // A name the enum does not have fails deserialization and never reaches here; a
        // number outside the range would not, which is what this catches.
        RuleFor(request => request.Status)
            .IsInEnum().WithMessage("Choose a status to move the ticket to.");

        // Assigned is a legal state — it is the workflow's first step — but this endpoint
        // carries no assignee, and a ticket that is Assigned to nobody would be a lie.
        // WP-1.6's assignment endpoint is what moves a ticket here, setting both at once.
        RuleFor(request => request.Status)
            .NotEqual(TicketStatus.Assigned)
            .WithMessage("Assign the ticket to a technician to move it to Assigned.");

        When(request => request.Status == TicketStatus.Resolved, () =>
            RuleFor(request => request.ResolutionNotes)
                .NotEmpty().WithMessage("Describe what was done to resolve the ticket.")
                .MaximumLength(Ticket.ResolutionNotesMaxLength));

        When(request => request.Status != TicketStatus.Resolved, () =>
            RuleFor(request => request.ResolutionNotes)
                .Null().WithMessage("Resolution notes are only recorded when resolving a ticket."));

        // The exact mirror, for holding. A ticket in Waiting is one nobody is working, and
        // the reason is what tells the next technician — and the requester reading their
        // own timeline — whether it is waiting on them.
        When(request => request.Status == TicketStatus.Waiting, () =>
            RuleFor(request => request.HoldReason)
                .NotEmpty().WithMessage("Say what the ticket is waiting on.")
                .MaximumLength(Ticket.HoldReasonMaxLength));

        When(request => request.Status != TicketStatus.Waiting, () =>
            RuleFor(request => request.HoldReason)
                .Null().WithMessage("A hold reason is only recorded when putting a ticket on hold."));
    }
}
