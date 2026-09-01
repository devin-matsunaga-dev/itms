namespace Itms.Modules.Directory.Features.Usage;

/// <summary>
/// What a location still holds, which is what an administrator is shown before being
/// offered a delete.
/// </summary>
/// <param name="LocationId">The location reported on.</param>
/// <param name="Name">Its own name.</param>
/// <param name="Path">Its full display path, so the answer names the room unambiguously.</param>
/// <param name="ChildCount">
/// How many locations sit directly beneath it. Kept separate from
/// <see cref="References"/> because it is Directory's own count and it blocks a delete
/// for a different reason — a subtree, rather than a reference from elsewhere.
/// </param>
/// <param name="References">
/// The per-module counts, ordered by entity name so the breakdown does not reshuffle
/// between reads. A module reporting zero is included: "no assets here" is an answer
/// the screen shows, and an absent row would be indistinguishable from a module that
/// has not been asked.
/// </param>
/// <param name="TotalReferences">The sum of <see cref="References"/>, so a caller deciding whether to offer a delete reads one number.</param>
/// <param name="CanDelete">
/// True when the location has no children and nothing references it. This is advisory —
/// the <c>DELETE</c> endpoint re-checks both, because the answer can change between the
/// two calls.
/// </param>
public sealed record LocationUsageResponse(
    Guid LocationId,
    string Name,
    string Path,
    int ChildCount,
    IReadOnlyList<UsageCountResponse> References,
    int TotalReferences,
    bool CanDelete);
