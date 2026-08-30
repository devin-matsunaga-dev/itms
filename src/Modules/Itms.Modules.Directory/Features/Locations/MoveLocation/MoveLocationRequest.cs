namespace Itms.Modules.Directory.Features.Locations.MoveLocation;

/// <summary>The body of <c>POST /api/v1/locations/{id}/move</c>.</summary>
/// <param name="ParentId">
/// The new parent, or <see langword="null"/> to move the node to the root — which only
/// an Organization may be.
/// </param>
public sealed record MoveLocationRequest(Guid? ParentId);
