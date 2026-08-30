using FluentValidation;
using Itms.Modules.Directory.Domain;

namespace Itms.Modules.Directory.Features.Locations.CreateLocation;

/// <summary>Checks the shape of a create request before the handler runs.</summary>
/// <remarks>
/// Placement is not checked here. Whether a Room may sit under this particular parent
/// depends on a row nobody has read yet, and the answer is a 409 about the hierarchy
/// rather than a 400 about the request.
/// </remarks>
public sealed class CreateLocationValidator : AbstractValidator<CreateLocationRequest>
{
    /// <summary>Builds the rules.</summary>
    public CreateLocationValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Enter a location name.")
            .MaximumLength(Location.NameMaxLength);

        RuleFor(request => request.Kind)
            .IsInEnum().WithMessage("Choose one of: Organization, Site, Building, Floor, Area, Room.");

        RuleFor(request => request.Description)
            .MaximumLength(Location.DescriptionMaxLength);
    }
}
