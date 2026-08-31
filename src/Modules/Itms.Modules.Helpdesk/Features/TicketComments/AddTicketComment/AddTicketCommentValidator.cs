using FluentValidation;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.TicketComments.AddTicketComment;

/// <summary>Checks the shape of a comment before the handler runs.</summary>
/// <remarks>
/// Only the body can be wrong on its own. Whether the ticket exists, whether the caller may
/// see it, and whether they may write an internal note are all facts about state and role
/// that are answered in the handler, where they are known.
/// </remarks>
public sealed class AddTicketCommentValidator : AbstractValidator<AddTicketCommentRequest>
{
    /// <summary>Builds the rules.</summary>
    public AddTicketCommentValidator() =>
        // Whitespace is not a comment. The entity trims before measuring, so the rule is
        // written against the trimmed length or an eight-thousand-and-one-space body would
        // be refused for the wrong reason.
        RuleFor(request => request.Body)
            .NotEmpty()
            .WithMessage("Write something before posting.")
            .Must(body => body is null || body.Trim().Length <= TicketComment.BodyMaxLength)
            .WithMessage($"A comment cannot be longer than {TicketComment.BodyMaxLength} characters.");
}
