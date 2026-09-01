using FluentValidation;
using Itms.Modules.Assets.Domain;

namespace Itms.Modules.Assets.Features.Assets.CreateAsset;

/// <summary>Checks the shape of a create request before the handler runs.</summary>
/// <remarks>
/// Uniqueness is not checked here: it needs the database, and a validator that queried one
/// would still lose the race to the unique index. The handler owns both the tag and the
/// per-manufacturer serial, and returns a 409 rather than a 400 — WP-2.1's done-criterion
/// names that status specifically.
/// </remarks>
public sealed class CreateAssetValidator : AbstractValidator<CreateAssetRequest>
{
    /// <summary>Builds the rules.</summary>
    public CreateAssetValidator()
    {
        RuleFor(request => request.AssetTag)
            .NotEmpty().WithMessage("Enter an asset tag.")
            .MaximumLength(AssetTagRules.MaxLength)
            .Must(AssetTagRules.IsWellFormed).WithMessage(AssetTagRules.Requirement);

        RuleFor(request => request.AssetTypeId)
            .NotEmpty().WithMessage("Choose an asset type.");

        RuleFor(request => request.Name).MaximumLength(Asset.NameMaxLength);
        RuleFor(request => request.SerialNumber).MaximumLength(Asset.SerialNumberMaxLength);
        RuleFor(request => request.Barcode).MaximumLength(Asset.BarcodeMaxLength);
        RuleFor(request => request.Manufacturer).MaximumLength(Asset.ManufacturerMaxLength);
        RuleFor(request => request.Model).MaximumLength(Asset.ModelMaxLength);
        RuleFor(request => request.Vendor).MaximumLength(Asset.VendorMaxLength);
        RuleFor(request => request.Notes).MaximumLength(Asset.NotesMaxLength);

        // A negative price is not a discount, it is a typo. Zero is allowed: donated and
        // written-down equipment is real.
        RuleFor(request => request.Cost)
            .GreaterThanOrEqualTo(0).WithMessage("A cost cannot be negative.")
            .When(request => request.Cost.HasValue);

        // The column is numeric(12,2); anything larger would be silently rounded or would
        // fail in the database with a message nobody can act on.
        RuleFor(request => request.Cost)
            .LessThan(10_000_000_000m).WithMessage("That cost is larger than this system records.")
            .When(request => request.Cost.HasValue);

        // Deliberately not a rule: a warranty that expired before the purchase date. It is
        // usually a typo, but second-hand equipment bought with the remainder of somebody
        // else's warranty is real, and refusing it would make honest data unenterable.
    }
}
