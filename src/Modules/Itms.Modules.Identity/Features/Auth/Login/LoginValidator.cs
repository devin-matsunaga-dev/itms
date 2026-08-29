using FluentValidation;

namespace Itms.Modules.Identity.Features.Auth.Login;

/// <summary>
/// Shape-checks the credentials before the handler runs.
/// </summary>
/// <remarks>
/// Deliberately weak: it asserts that something was sent and that it is not absurdly
/// long, and nothing else. Validating a submitted password against the password policy
/// would tell an attacker which of their guesses were even eligible.
/// </remarks>
public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    /// <summary>Builds the rules.</summary>
    public LoginValidator()
    {
        RuleFor(request => request.UserName)
            .NotEmpty().WithMessage("Enter your user name or email address.")
            .MaximumLength(320);

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Enter your password.")
            .MaximumLength(256);
    }
}
