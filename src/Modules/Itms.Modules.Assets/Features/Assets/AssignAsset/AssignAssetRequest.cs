using FluentValidation;
using Itms.Modules.Assets.Domain;

namespace Itms.Modules.Assets.Features.Assets.AssignAsset;

/// <summary>
/// Who is to hold the asset, or nobody.
/// </summary>
/// <remarks>
/// <b>One shape for issue, transfer, and return</b>, following the call WP-1.6 made for a
/// ticket's assignment: they are the same fact — who holds this — read at three moments,
/// and a null <see cref="AssignedToUserId"/> is the asset coming back rather than a missing
/// field. Three endpoints would mean three routes to one column and three chances to forget
/// the history entry.
/// </remarks>
/// <param name="AssignedToUserId">Who is taking it on, or <see langword="null"/> to take it back off whoever has it.</param>
/// <param name="Note">What the operator wants recorded against this move, or <see langword="null"/>.</param>
public sealed record AssignAssetRequest(Guid? AssignedToUserId, string? Note);

/// <summary>Checks the shape of an assignment request before the handler runs.</summary>
/// <remarks>
/// Whether the account exists, is active, and is not already holding the asset are all
/// facts about rows, so the handler answers them — the first two through
/// <c>IUserLookup</c>, because they are Identity's to know.
/// </remarks>
public sealed class AssignAssetValidator : AbstractValidator<AssignAssetRequest>
{
    /// <summary>Builds the rules.</summary>
    public AssignAssetValidator()
    {
        // Present-but-empty is a client bug: it means the caller meant to name somebody and
        // sent a default Guid. Absent is the deliberate "take it back" case and is fine.
        RuleFor(request => request.AssignedToUserId)
            .NotEqual(Guid.Empty).WithMessage("Choose who is taking the asset on.")
            .When(request => request.AssignedToUserId.HasValue);

        RuleFor(request => request.Note).MaximumLength(AssetHistoryEntry.NoteMaxLength);
    }
}
