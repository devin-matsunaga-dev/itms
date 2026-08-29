namespace Itms.Contracts.Lookups;

/// <summary>A department, as other modules see it.</summary>
/// <param name="Id">The department's id.</param>
/// <param name="Name">Its display name.</param>
/// <param name="IsActive">False once retired; existing tickets and users keep referencing it.</param>
public sealed record DepartmentSummary(Guid Id, string Name, bool IsActive);

/// <summary>
/// A location, as other modules see it, including its full path.
/// </summary>
/// <param name="Id">The location's id.</param>
/// <param name="Name">The leaf name, such as a room number.</param>
/// <param name="Path">
/// The full Organization → Site → Building → Floor → Room path as display text.
/// Alerts copy this rather than referencing it, because invariant 7 requires the
/// location context an alert was raised with to survive a later rename or move.
/// </param>
/// <param name="ParentId">The parent node, or <see langword="null"/> at the root.</param>
public sealed record LocationSummary(Guid Id, string Name, string Path, Guid? ParentId);

/// <summary>How other modules read departments. Owned by <c>Modules.Directory</c>.</summary>
public interface IDepartmentLookup
{
    /// <summary>The department with <paramref name="departmentId"/>, or <see langword="null"/>.</summary>
    Task<DepartmentSummary?> GetAsync(Guid departmentId, CancellationToken cancellationToken);

    /// <summary>The departments in <paramref name="departmentIds"/> that exist, batched for list screens.</summary>
    Task<IReadOnlyList<DepartmentSummary>> GetManyAsync(IReadOnlyCollection<Guid> departmentIds, CancellationToken cancellationToken);
}

/// <summary>How other modules read locations. Owned by <c>Modules.Directory</c>.</summary>
public interface ILocationLookup
{
    /// <summary>The location with <paramref name="locationId"/>, or <see langword="null"/>.</summary>
    Task<LocationSummary?> GetAsync(Guid locationId, CancellationToken cancellationToken);

    /// <summary>The locations in <paramref name="locationIds"/> that exist, batched for list screens.</summary>
    Task<IReadOnlyList<LocationSummary>> GetManyAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken);
}
