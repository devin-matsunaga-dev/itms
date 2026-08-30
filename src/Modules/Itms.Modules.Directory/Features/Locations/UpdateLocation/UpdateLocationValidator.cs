using FluentValidation;
using Itms.Modules.Directory.Domain;

namespace Itms.Modules.Directory.Features.Locations.UpdateLocation;

/// <summary>Checks the shape of an update request before the handler runs.</summary>
public sealed class UpdateLocationValidator : AbstractValidator<UpdateLocationRequest>
{
    /// <summary>Builds the rules.</summary>
    public UpdateLocationValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Enter a location name.")
            .MaximumLength(Location.NameMaxLength);

        RuleFor(request => request.Description)
            .MaximumLength(Location.DescriptionMaxLength);
    }
}
