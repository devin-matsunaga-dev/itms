using Itms.Modules.Directory.Domain;

namespace Itms.Modules.Directory.Features.Locations;

/// <summary>A location as the API renders it.</summary>
/// <param name="Id">The node's id.</param>
/// <param name="Name">Its own name — "Room G-04", not the whole path.</param>
/// <param name="Kind">Which level of the hierarchy it is.</param>
/// <param name="ParentId">Its parent, or <see langword="null"/> at the root.</param>
/// <param name="Path">The full display path from the root, which is what other modules also receive.</param>
/// <param name="Depth">How far below the root it sits.</param>
/// <param name="Description">Free text, or <see langword="null"/>.</param>
/// <param name="ChildCount">
/// How many nodes sit directly beneath it. Carried so a caller can render the tree and
/// know whether a delete will be refused before it tries.
/// </param>
/// <param name="CreatedAt">When it was created (UTC).</param>
/// <param name="UpdatedAt">When it was last changed (UTC).</param>
public sealed record LocationResponse(
    Guid Id,
    string Name,
    LocationKind Kind,
    Guid? ParentId,
    string Path,
    int Depth,
    string? Description,
    int ChildCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
