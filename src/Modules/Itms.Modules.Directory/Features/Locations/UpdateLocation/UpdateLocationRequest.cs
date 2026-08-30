namespace Itms.Modules.Directory.Features.Locations.UpdateLocation;

/// <summary>The body of <c>PUT /api/v1/locations/{id}</c>.</summary>
/// <remarks>
/// Name and description only. Reparenting is <c>POST /{id}/move</c> and changing the
/// kind is neither — a room does not become a building, and allowing it here would let
/// an edit form silently invert a subtree.
/// </remarks>
/// <param name="Name">The node's own name.</param>
/// <param name="Description">Free text, or <see langword="null"/> to clear it.</param>
public sealed record UpdateLocationRequest(string Name, string? Description);
