using System.Linq.Expressions;
using FluentValidation;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.TicketPriorities;

/// <summary>
/// The SLA-target rules create and update share.
/// </summary>
/// <remarks>
/// The entity guards the same invariants and throws, which is the backstop for a caller
/// inside the module. These exist so a person filling in a form gets a 400 with the
/// message on the offending field instead of a 500.
/// <para>
/// This is bounds checking, not SLA arithmetic. Nothing here knows what a target
/// <em>means</em> — that is WP-1.8.
/// </para>
/// </remarks>
internal static class TicketPriorityTargetRules
{
    /// <summary>Adds the target rules to a validator.</summary>
    /// <typeparam name="TRequest">The request being validated.</typeparam>
    /// <param name="validator">The validator to extend.</param>
    /// <param name="response">Selects the response target.</param>
    /// <param name="resolution">Selects the resolution target.</param>
    public static void Apply<TRequest>(
        AbstractValidator<TRequest> validator,
        Expression<Func<TRequest, int>> response,
        Expression<Func<TRequest, int>> resolution)
    {
        ArgumentNullException.ThrowIfNull(validator);

        validator.RuleFor(response)
            .InclusiveBetween(1, TicketPriority.MaxTargetMinutes)
            .WithMessage($"A response target must be between 1 and {TicketPriority.MaxTargetMinutes} minutes.");

        validator.RuleFor(resolution)
            .InclusiveBetween(1, TicketPriority.MaxTargetMinutes)
            .WithMessage($"A resolution target must be between 1 and {TicketPriority.MaxTargetMinutes} minutes.");

        // The expression overload, not the compiled one: FluentValidation reads the
        // member name out of it for the error's field key, which is what lets the client
        // put the message on the resolution input rather than at the top of the form.
        validator.RuleFor(resolution)
            .GreaterThanOrEqualTo(response)
            .WithMessage("A resolution target cannot be sooner than the response target.");
    }
}
