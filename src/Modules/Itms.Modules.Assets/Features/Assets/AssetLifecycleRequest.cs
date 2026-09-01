using FluentValidation;
using Itms.Modules.Assets.Domain;

namespace Itms.Modules.Assets.Features.Assets;

/// <summary>
/// The body of a lifecycle operation that names no other party: sending an asset for
/// repair, returning it to service, retiring it.
/// </summary>
/// <remarks>
/// <para>
/// One shape shared by three endpoints rather than three identical single-field records.
/// They ask the same question and a client should not have to learn three names for it;
/// splitting them would also put three copies of the note's length rule in the generated
/// types, which is exactly the kind of drift a generated client is supposed to prevent.
/// Assignment is not one of them — it names a person, so it has its own shape.
/// </para>
/// <para>
/// <b>The note is optional</b>, unlike WP-1.15's hold reason. A technician parking a ticket
/// is being asked to justify a queue position; somebody booking a box of laptops back in
/// from repair is not, and requiring a note would produce a column full of full stops. Where
/// there is something to say — which vendor, what failed, who authorised the write-off — it
/// belongs on the entry and travels with it forever.
/// </para>
/// </remarks>
/// <param name="Note">What the operator wants recorded against this move, or <see langword="null"/>.</param>
public sealed record AssetLifecycleRequest(string? Note);

/// <summary>Checks the shape of a lifecycle request before the handler runs.</summary>
/// <remarks>
/// Whether the move itself is legal is not asked here: it depends on the asset's current
/// status, which needs the database, and the answer is a 409 rather than a 400 — the
/// request is well formed and it is the asset's state that refuses it.
/// </remarks>
public sealed class AssetLifecycleRequestValidator : AbstractValidator<AssetLifecycleRequest>
{
    /// <summary>Builds the rules.</summary>
    public AssetLifecycleRequestValidator()
    {
        RuleFor(request => request.Note).MaximumLength(AssetHistoryEntry.NoteMaxLength);
    }
}
