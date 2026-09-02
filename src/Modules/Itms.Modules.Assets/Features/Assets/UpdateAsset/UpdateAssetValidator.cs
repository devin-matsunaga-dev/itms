using FluentValidation;
using Itms.Modules.Assets.Domain;

namespace Itms.Modules.Assets.Features.Assets.UpdateAsset;

/// <summary>Checks the shape of an edit request before the handler runs.</summary>
/// <remarks>
/// <para>
/// The same bounds <c>CreateAssetValidator</c> applies, minus the tag — which this shape
/// does not carry — because the columns they guard are the same columns. Written out
/// rather than shared with the create's validator: the two requests are different types
/// and FluentValidation binds to one, and a base class holding five <c>MaximumLength</c>
/// calls would be indirection bought at the price of nothing.
/// </para>
/// <para>
/// Serial uniqueness per manufacturer is not checked here, for the reason the create's own
/// remarks give: it needs the database, and a validator that queried one would still lose
/// the race to the unique index. The handler owns it and answers 409.
/// </para>
/// </remarks>
public sealed class UpdateAssetValidator : AbstractValidator<UpdateAssetRequest>
{
    /// <summary>Builds the rules.</summary>
    public UpdateAssetValidator()
    {
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

        // Deliberately not a rule, as on the create: a warranty that expired before the
        // purchase date is usually a typo, but second-hand equipment bought with the
        // remainder of somebody else's warranty is real.
    }
}
