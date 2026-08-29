using FluentValidation;
using Itms.Modules.Identity.Security;
using Microsoft.Extensions.Options;

namespace Itms.Modules.Identity.Features.Auth.ChangePassword;

/// <summary>
/// Checks the obvious before the handler runs. The full password policy is still
/// enforced by <c>UserManager</c> — this only spares the caller a round trip for the
/// mistakes that need no database to spot.
/// </summary>
public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    /// <summary>Builds the rules.</summary>
    /// <param name="options">The configured authentication options.</param>
    public ChangePasswordValidator(IOptions<ItmsAuthOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        RuleFor(request => request.CurrentPassword)
            .NotEmpty().WithMessage("Enter your current password.");

        RuleFor(request => request.NewPassword)
            .NotEmpty().WithMessage("Enter a new password.")
            .MinimumLength(options.Value.MinimumPasswordLength)
                .WithMessage($"Use at least {options.Value.MinimumPasswordLength} characters.")
            .MaximumLength(256);

        RuleFor(request => request.NewPassword)
            .NotEqual(request => request.CurrentPassword)
            .WithMessage("The new password must be different from the current one.");
    }
}
