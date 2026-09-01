using FluentValidation;
using Itms.Modules.Assets.Domain;

namespace Itms.Modules.Assets.Features.AssetStatuses.UpdateAssetStatus;

/// <summary>Checks the shape of an update request before the handler runs.</summary>
public sealed class UpdateAssetStatusValidator : AbstractValidator<UpdateAssetStatusRequest>
{
    /// <summary>Builds the rules.</summary>
    public UpdateAssetStatusValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Enter an asset status name.")
            .MaximumLength(AssetStatus.NameMaxLength);

        RuleFor(request => request.Description)
            .MaximumLength(AssetStatus.DescriptionMaxLength);

        RuleFor(request => request.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("A sort order cannot be negative.");
    }
}
