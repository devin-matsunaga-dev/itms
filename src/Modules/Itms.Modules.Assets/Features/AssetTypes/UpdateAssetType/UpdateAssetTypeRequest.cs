namespace Itms.Modules.Assets.Features.AssetTypes.UpdateAssetType;

/// <summary>The fields an asset type is edited to.</summary>
/// <param name="Name">Its new display name.</param>
/// <param name="Description">What belongs in it, or <see langword="null"/> to clear it.</param>
/// <param name="SortOrder">Where it sits in a picker.</param>
public sealed record UpdateAssetTypeRequest(string Name, string? Description, int SortOrder);
