namespace Itms.Contracts.Lookups;

/// <summary>
/// How many rows of one kind reference a single department or location.
/// </summary>
/// <param name="EntityName">
/// What is being counted, in lower-case plural — <c>assets</c>, <c>tickets</c>,
/// <c>users</c>. It is rendered to an administrator and used to sort the breakdown, so
/// it is a stable string: add a new one rather than reword an existing one.
/// </param>
/// <param name="Count">How many of them reference the entry. Never negative; zero is a legitimate answer.</param>
public sealed record DirectoryUsage(string EntityName, int Count);

/// <summary>
/// How a module reports its own references to a department or a location, so that
/// Directory can say what a delete would orphan.
/// </summary>
/// <remarks>
/// <para>
/// This runs the opposite way to the other lookups in this namespace. <c>IAssetLookup</c>
/// lets other modules read Assets; this lets Assets answer a question <em>about</em>
/// Directory's rows that only Assets can answer, because §3 rule 6 stores the reference
/// as a plain identifier with no foreign key — the database cannot be asked, and rule 1
/// forbids Directory from querying <c>assets.assets</c> itself.
/// </para>
/// <para>
/// Directory takes every registered implementation and fans out. A module that gains a
/// <c>department_id</c> or <c>location_id</c> column adds an implementation here, and a
/// module that does not have one simply does not register one — a directory entry with
/// no counters reports a total of zero, which is correct rather than merely convenient.
/// </para>
/// <para>
/// Implementations count the rows a reader can actually see: soft-deleted rows are
/// excluded, because a delete cannot orphan a row nothing renders. Deactivated rows are
/// counted, because they are still read — invariant 9 keeps a deactivated user's record,
/// and that record still shows the location it points at.
/// </para>
/// </remarks>
public interface IDirectoryUsageLookup
{
    /// <summary>How many rows reference <paramref name="departmentId"/>.</summary>
    /// <param name="departmentId">The department being reported on.</param>
    /// <param name="cancellationToken">Cancels the count.</param>
    /// <returns>The count, or zero when this module holds no department reference at all.</returns>
    Task<DirectoryUsage> CountForDepartmentAsync(Guid departmentId, CancellationToken cancellationToken);

    /// <summary>How many rows reference <paramref name="locationId"/>.</summary>
    /// <param name="locationId">The location being reported on. Exactly this node — a
    /// subtree is never counted, because only a childless node can be deleted.</param>
    /// <param name="cancellationToken">Cancels the count.</param>
    /// <returns>The count, or zero when this module holds no location reference at all.</returns>
    Task<DirectoryUsage> CountForLocationAsync(Guid locationId, CancellationToken cancellationToken);
}
