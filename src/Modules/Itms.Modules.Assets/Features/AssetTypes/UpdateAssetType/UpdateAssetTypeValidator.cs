using FluentValidation;
using Itms.Modules.Assets.Domain;

namespace Itms.Modules.Assets.Features.AssetTypes.UpdateAssetType;

/// <summary>Checks the shape of an update request before the handler runs.</summary>
public sealed class UpdateAssetTypeValidator : AbstractValidator<UpdateAssetTypeRequest>
{
    /// <summary>Builds the rules.</summary>
    public UpdateAssetTypeValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Enter an asset type name.")
            .MaximumLength(AssetType.NameMaxLength);

        RuleFor(request => request.Description)
            .MaximumLength(AssetType.DescriptionMaxLength);

        RuleFor(request => request.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("A sort order cannot be negative.");
    }
}
