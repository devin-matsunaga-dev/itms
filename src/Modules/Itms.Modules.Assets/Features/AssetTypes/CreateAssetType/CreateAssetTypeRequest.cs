namespace Itms.Modules.Assets.Features.AssetTypes.CreateAssetType;

/// <summary>The fields a new asset type is created from.</summary>
/// <param name="Name">Its display name.</param>
/// <param name="Description">What belongs in it, or <see langword="null"/>.</param>
/// <param name="SortOrder">Where it sits in a picker.</param>
public sealed record CreateAssetTypeRequest(string Name, string? Description, int SortOrder);
