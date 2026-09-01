using FluentValidation;
using Itms.Modules.Assets.Domain;

namespace Itms.Modules.Assets.Features.AssetStatuses.CreateAssetStatus;

/// <summary>Checks the shape of a create request before the handler runs.</summary>
/// <remarks>
/// Uniqueness is not checked here: it needs the database, and a validator that queried one
/// would still lose the race to the unique index. The handler owns that, and returns a 409
/// rather than a 400.
/// </remarks>
public sealed class CreateAssetStatusValidator : AbstractValidator<CreateAssetStatusRequest>
{
    /// <summary>Builds the rules.</summary>
    public CreateAssetStatusValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty().WithMessage("Enter a status code.")
            .Must(AssetStatusCode.IsWellFormed).WithMessage(AssetStatusCode.Requirement);

        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Enter an asset status name.")
            .MaximumLength(AssetStatus.NameMaxLength);

        RuleFor(request => request.Description)
            .MaximumLength(AssetStatus.DescriptionMaxLength);

        RuleFor(request => request.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("A sort order cannot be negative.");
    }
}
