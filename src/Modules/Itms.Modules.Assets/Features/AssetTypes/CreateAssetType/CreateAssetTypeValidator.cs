using FluentValidation;
using Itms.Modules.Assets.Domain;

namespace Itms.Modules.Assets.Features.AssetTypes.CreateAssetType;

/// <summary>Checks the shape of a create request before the handler runs.</summary>
/// <remarks>
/// Uniqueness is not checked here: it needs the database, and a validator that queried one
/// would still lose the race to the unique index. The handler owns that, and returns a 409
/// rather than a 400.
/// </remarks>
public sealed class CreateAssetTypeValidator : AbstractValidator<CreateAssetTypeRequest>
{
    /// <summary>Builds the rules.</summary>
    public CreateAssetTypeValidator()
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
