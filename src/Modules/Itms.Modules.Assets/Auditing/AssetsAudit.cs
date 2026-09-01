using Itms.Contracts.Auditing;

namespace Itms.Modules.Assets.Auditing;

/// <summary>
/// The action identifiers this module writes through <c>IAuditWriter</c>, and the small
/// helpers its handlers build a diff with.
/// </summary>
/// <remarks>
/// <para>
/// Asset types and statuses change through plain handlers and raise no domain event —
/// ARCHITECTURE.md §5 names none and nothing consumes one — so they are exactly the
/// "mutations that do not warrant a domain event" §8 keeps <c>IAuditWriter</c> for.
/// SPEC.md §15 counts them as administrative configuration changes, which are mandatory
/// coverage; asset modifications are named there in their own right.
/// </para>
/// <para>
/// <b>Creating an asset is audited here and raises no event, deliberately.</b>
/// ARCHITECTURE.md §5's event list names <c>AssetAssigned</c> and
/// <c>AssetStatusChanged</c> and no <c>AssetCreated</c>, and the Audit module binds a
/// consumer to each of the two that exist. The event is the route for those two;
/// <c>IAuditWriter</c> is the route for everything else.
/// </para>
/// <para>
/// <b>WP-2.2 published both events and deliberately declared no action string for either,
/// which is the trap WP-1.3 set and WP-1.6 had to go back and defuse in Helpdesk.</b>
/// <c>AssetLifecycleMutation</c> publishes; the Audit module's consumer derives
/// <c>asset.assigned</c> and <c>asset.status_changed</c> from what it publishes. <b>Adding
/// an assignment or lifecycle action to this file would make every such change record two
/// rows saying the same thing.</b> There is no gap here to fill — a change that looks
/// unaudited is being audited on the outbox, one dispatcher pass later.
/// </para>
/// <para>
/// <b>What that costs, recorded where somebody will look for it.</b> An event-derived row
/// carries no source IP and no actor name, because the dispatcher runs on a background
/// scope with no principal — WP-1.6 paid the same price for the ticket actions. The asset's
/// own timeline is what keeps both: <c>AssetHistoryEntry</c> is written inside the request
/// by the handler and caches the actor's name at the time.
/// </para>
/// <para>
/// The names are declared here rather than shared with the Audit module because a module
/// may not reference another module (§3 rule 2) — this is the fourth module to do so, after
/// Identity, Directory, and Helpdesk. They are stable strings stored in a text column: add
/// rather than rename. The convention every module follows is
/// <c>&lt;module&gt;.&lt;entity&gt;_&lt;past-tense verb&gt;</c>, all lower snake case.
/// </para>
/// </remarks>
internal static class AssetsAudit
{
    /// <summary>An asset was recorded.</summary>
    public const string AssetCreated = "assets.asset_created";

    /// <summary>An asset type was created.</summary>
    public const string AssetTypeCreated = "assets.asset_type_created";

    /// <summary>An asset type's name, description, or order changed.</summary>
    public const string AssetTypeUpdated = "assets.asset_type_updated";

    /// <summary>An asset type was retired.</summary>
    public const string AssetTypeRetired = "assets.asset_type_retired";

    /// <summary>A retired asset type was brought back.</summary>
    public const string AssetTypeReinstated = "assets.asset_type_reinstated";

    /// <summary>An asset status was created.</summary>
    public const string AssetStatusCreated = "assets.asset_status_created";

    /// <summary>An asset status's name, description, or order changed.</summary>
    public const string AssetStatusUpdated = "assets.asset_status_updated";

    /// <summary>An asset status was retired.</summary>
    public const string AssetStatusRetired = "assets.asset_status_retired";

    /// <summary>A retired asset status was brought back.</summary>
    public const string AssetStatusReinstated = "assets.asset_status_reinstated";

    /// <summary>The entity type of an asset entry.</summary>
    public const string AssetEntityType = "Asset";

    /// <summary>The entity type of an asset-type entry.</summary>
    public const string AssetTypeEntityType = "AssetType";

    /// <summary>The entity type of an asset-status entry.</summary>
    public const string AssetStatusEntityType = "AssetStatus";

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
    /// Records a field only when it actually moved. ARCHITECTURE.md §8 wants changed fields
    /// only, and an edit form that posts every field would otherwise make every entry look
    /// like a rewrite of the whole row.
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
