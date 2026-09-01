using Itms.Modules.Assets.Domain;

namespace Itms.Modules.Assets.Features.AssetStatuses;

/// <summary>An asset status as the API renders it.</summary>
/// <param name="Id">The status's id. What an asset stores.</param>
/// <param name="Code">Its stable machine identifier. Never changes, unlike the name.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Description">What it means, or <see langword="null"/>.</param>
/// <param name="SortOrder">Where it sits in a picker.</param>
/// <param name="IsActive">False once retired.</param>
/// <param name="CreatedAt">When it was created (UTC).</param>
/// <param name="UpdatedAt">When it was last changed (UTC).</param>
public sealed record AssetStatusResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>The projection every status query uses, so one shape is built in one place.</summary>
    internal static System.Linq.Expressions.Expression<Func<AssetStatus, AssetStatusResponse>> Projection() =>
        status => new AssetStatusResponse(
            status.Id,
            status.Code,
            status.Name,
            status.Description,
            status.SortOrder,
            status.IsActive,
            status.CreatedAt,
            status.UpdatedAt);

    /// <summary>Renders an entity the handler already has in memory.</summary>
    /// <param name="status">The status to render.</param>
    /// <returns>The response shape.</returns>
    internal static AssetStatusResponse From(AssetStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new AssetStatusResponse(
            status.Id,
            status.Code,
            status.Name,
            status.Description,
            status.SortOrder,
            status.IsActive,
            status.CreatedAt,
            status.UpdatedAt);
    }
}
