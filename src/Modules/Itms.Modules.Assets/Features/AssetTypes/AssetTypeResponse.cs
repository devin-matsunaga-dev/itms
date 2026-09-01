using Itms.Modules.Assets.Domain;

namespace Itms.Modules.Assets.Features.AssetTypes;

/// <summary>An asset type as the API renders it.</summary>
/// <param name="Id">The type's id. What an asset stores, and what a rename does not change.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Description">What belongs in it, or <see langword="null"/>.</param>
/// <param name="SortOrder">Where it sits in a picker.</param>
/// <param name="IsActive">False once retired.</param>
/// <param name="CreatedAt">When it was created (UTC).</param>
/// <param name="UpdatedAt">When it was last changed (UTC).</param>
public sealed record AssetTypeResponse(
    Guid Id,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>The projection every type query uses, so one shape is built in one place.</summary>
    internal static System.Linq.Expressions.Expression<Func<AssetType, AssetTypeResponse>> Projection() =>
        type => new AssetTypeResponse(
            type.Id,
            type.Name,
            type.Description,
            type.SortOrder,
            type.IsActive,
            type.CreatedAt,
            type.UpdatedAt);

    /// <summary>Renders an entity the handler already has in memory.</summary>
    /// <param name="type">The type to render.</param>
    /// <returns>The response shape.</returns>
    internal static AssetTypeResponse From(AssetType type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return new AssetTypeResponse(
            type.Id,
            type.Name,
            type.Description,
            type.SortOrder,
            type.IsActive,
            type.CreatedAt,
            type.UpdatedAt);
    }
}
