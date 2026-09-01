namespace Itms.Modules.Assets.Features.AssetStatuses.CreateAssetStatus;

/// <summary>The fields a new asset status is created from.</summary>
/// <param name="Code">Its stable machine identifier. Set once and never changed.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Description">What it means, or <see langword="null"/>.</param>
/// <param name="SortOrder">Where it sits in a picker.</param>
public sealed record CreateAssetStatusRequest(string Code, string Name, string? Description, int SortOrder);
