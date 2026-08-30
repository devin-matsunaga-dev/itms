using Itms.Modules.Directory.Domain;

namespace Itms.Modules.Directory.Features.Departments;

/// <summary>A department as the API renders it.</summary>
/// <remarks>
/// Wider than <c>DepartmentSummary</c>, which is what other modules see: this carries the
/// administrative fields the directory screen edits, and that surface stays out of the
/// cross-module contract deliberately.
/// </remarks>
/// <param name="Id">The department's id.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Code">Its short code, or <see langword="null"/>.</param>
/// <param name="Description">Free text, or <see langword="null"/>.</param>
/// <param name="IsActive">False once retired.</param>
/// <param name="CreatedAt">When it was created (UTC).</param>
/// <param name="UpdatedAt">When it was last changed (UTC).</param>
public sealed record DepartmentResponse(
    Guid Id,
    string Name,
    string? Code,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>The projection every department query uses, so one shape is built in one place.</summary>
    internal static System.Linq.Expressions.Expression<Func<Department, DepartmentResponse>> Projection() =>
        department => new DepartmentResponse(
            department.Id,
            department.Name,
            department.Code,
            department.Description,
            department.IsActive,
            department.CreatedAt,
            department.UpdatedAt);
}
