using Itms.Contracts.Auditing;

namespace Itms.Modules.Directory.Auditing;

/// <summary>
/// The action identifiers this module writes through <c>IAuditWriter</c>, and the small
/// helpers its handlers build a diff with.
/// </summary>
/// <remarks>
/// <para>
/// Departments and locations change through plain handlers and raise no domain event —
/// nothing consumes one yet — so they are exactly the "mutations that do not warrant a
/// domain event" ARCHITECTURE.md §8 keeps <c>IAuditWriter</c> for. SPEC.md §15 counts
/// them as administrative changes, which are mandatory coverage.
/// </para>
/// <para>
/// The names are declared here rather than shared with the Audit module because a module
/// may not reference another module (§3 rule 2). They are stable strings stored in a text
/// column: add rather than rename.
/// </para>
/// </remarks>
internal static class DirectoryAudit
{
    /// <summary>A department was created.</summary>
    public const string DepartmentCreated = "directory.department_created";

    /// <summary>A department's name, code, or description changed.</summary>
    public const string DepartmentUpdated = "directory.department_updated";

    /// <summary>A department was retired.</summary>
    public const string DepartmentRetired = "directory.department_retired";

    /// <summary>A retired department was brought back.</summary>
    public const string DepartmentReinstated = "directory.department_reinstated";

    /// <summary>A location was created.</summary>
    public const string LocationCreated = "directory.location_created";

    /// <summary>A location's name or description changed.</summary>
    public const string LocationUpdated = "directory.location_updated";

    /// <summary>A location was reparented, carrying its subtree.</summary>
    public const string LocationMoved = "directory.location_moved";

    /// <summary>A leaf location was deleted.</summary>
    public const string LocationDeleted = "directory.location_deleted";

    /// <summary>The entity type of a department entry.</summary>
    public const string DepartmentEntityType = "Department";

    /// <summary>The entity type of a location entry.</summary>
    public const string LocationEntityType = "Location";

    /// <summary>Starts a diff.</summary>
    /// <returns>An empty, ordinal-keyed change set.</returns>
    public static Dictionary<string, AuditFieldChange> Changes() => new(StringComparer.Ordinal);

    /// <summary>Records a field as newly set — the create case, where there is no before.</summary>
    /// <param name="changes">The diff being built.</param>
    /// <param name="field">The field name, camel-cased as the client sees it.</param>
    /// <param name="value">The value it was set to.</param>
    /// <returns>The diff, for chaining.</returns>
    public static Dictionary<string, AuditFieldChange> Set(
        this Dictionary<string, AuditFieldChange> changes,
        string field,
        string? value)
    {
        ArgumentNullException.ThrowIfNull(changes);
        changes[field] = new AuditFieldChange(null, value);
        return changes;
    }

    /// <summary>
    /// Records a field only when it actually moved. ARCHITECTURE.md §8 wants changed
    /// fields only, and an edit form that posts every field would otherwise make every
    /// entry look like a rewrite of the whole row.
    /// </summary>
    /// <param name="changes">The diff being built.</param>
    /// <param name="field">The field name, camel-cased as the client sees it.</param>
    /// <param name="before">The value before the edit.</param>
    /// <param name="after">The value after it.</param>
    /// <returns>The diff, for chaining.</returns>
    public static Dictionary<string, AuditFieldChange> Moved(
        this Dictionary<string, AuditFieldChange> changes,
        string field,
        string? before,
        string? after)
    {
        ArgumentNullException.ThrowIfNull(changes);

        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            changes[field] = new AuditFieldChange(before, after);
        }

        return changes;
    }
}
