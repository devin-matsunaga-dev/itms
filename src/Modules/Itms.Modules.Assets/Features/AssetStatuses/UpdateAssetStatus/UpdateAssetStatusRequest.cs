namespace Itms.Modules.Assets.Features.AssetStatuses.UpdateAssetStatus;

/// <summary>
/// The fields an asset status is edited to.
/// </summary>
/// <remarks>
/// There is no code here, deliberately: the code is what WP-2.2's lifecycle methods and
/// DESIGN.md's colours key off, and a value an administrator can move is a value neither
/// can depend on. A rename changes the name alone.
/// </remarks>
/// <param name="Name">Its new display name.</param>
/// <param name="Description">What it means, or <see langword="null"/> to clear it.</param>
/// <param name="SortOrder">Where it sits in a picker.</param>
public sealed record UpdateAssetStatusRequest(string Name, string? Description, int SortOrder);
