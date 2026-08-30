using Itms.Modules.Directory.Domain;

namespace Itms.Modules.Directory.Features.Locations.CreateLocation;

/// <summary>The body of <c>POST /api/v1/locations</c>.</summary>
/// <param name="Name">The node's own name. Unique among its siblings, case-insensitively.</param>
/// <param name="Kind">Which level of the hierarchy it is.</param>
/// <param name="ParentId">The parent node, or <see langword="null"/> to create a root organisation.</param>
/// <param name="Description">Free text, or <see langword="null"/>.</param>
public sealed record CreateLocationRequest(
    string Name,
    LocationKind Kind,
    Guid? ParentId,
    string? Description);
