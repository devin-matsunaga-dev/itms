namespace Itms.Modules.Directory.Features.Usage;

/// <summary>
/// What a department still holds.
/// </summary>
/// <remarks>
/// There is no <c>CanDelete</c> here and no delete endpoint to guard: WP-0.6 settled that
/// a department is retired rather than deleted, precisely so that every historical
/// reference keeps resolving. This read is what tells an administrator what retiring one
/// will affect, and it never blocks the retirement — a department with three hundred
/// tickets against it is exactly the department that must be retired rather than removed.
/// </remarks>
/// <param name="DepartmentId">The department reported on.</param>
/// <param name="Name">Its display name.</param>
/// <param name="IsActive">False once retired.</param>
/// <param name="References">The per-module counts, ordered by entity name. Modules reporting zero are included.</param>
/// <param name="TotalReferences">The sum of <see cref="References"/>.</param>
public sealed record DepartmentUsageResponse(
    Guid DepartmentId,
    string Name,
    bool IsActive,
    IReadOnlyList<UsageCountResponse> References,
    int TotalReferences);
