using FluentValidation;
using Itms.Modules.Monitoring.Domain;

namespace Itms.Modules.Monitoring.Features.Devices.SetSnmpCredential;

/// <summary>Checks the shape of a credential write before the handler runs.</summary>
/// <remarks>
/// It checks that something was sent and that it fits the column, and nothing else. There
/// is no complexity rule: a community string has to match whatever the device on the other
/// end was configured with, and a system that refused the one an operator was given would
/// simply be unusable.
/// </remarks>
public sealed class SetSnmpCredentialValidator : AbstractValidator<SetSnmpCredentialRequest>
{
    /// <summary>Builds the rules.</summary>
    public SetSnmpCredentialValidator()
    {
        RuleFor(request => request.Community)
            .NotEmpty().WithMessage("Enter the read-only community string.")
            .MaximumLength(SnmpSettings.CommunityMaxLength);
    }
}
