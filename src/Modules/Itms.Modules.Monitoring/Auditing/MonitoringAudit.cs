using Itms.Contracts.Auditing;

namespace Itms.Modules.Monitoring.Auditing;

/// <summary>
/// The action identifiers this module writes through <c>IAuditWriter</c>, and the small
/// helpers its handlers build a diff with.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every device mutation is audited here, and none of them raises a domain event.</b>
/// ARCHITECTURE.md §5's event list names <c>DeviceWentOffline</c> and
/// <c>DeviceRecovered</c> and nothing else for this module — both are state transitions the
/// poller's results cause, and <c>WP-3.3</c> is the package that publishes them.
/// Registering, correcting and switching a device are administrative configuration changes,
/// which SPEC.md §15 makes mandatory audit coverage and §8 keeps <c>IAuditWriter</c> for.
/// </para>
/// <para>
/// <b>WP-3.3 must not add an action string for either event.</b> The Audit module has bound
/// a consumer to both since WP-0.7 and derives its own entry from what is published, so a
/// writer call beside the publish would record every transition twice — the trap WP-1.3 set
/// in Helpdesk and WP-1.6 had to go back and defuse. A transition that looks unaudited is
/// being audited on the outbox, one dispatcher pass later.
/// </para>
/// <para>
/// <b>The SNMP community string never appears in an entry.</b>
/// <see cref="DeviceSnmpCredentialSet"/> and <see cref="DeviceSnmpCredentialCleared"/>
/// record that a credential moved and carry no value on either side of the diff, because an
/// audit trail somebody can read a secret out of is a second copy of the secret — and one
/// with a longer retention and a wider readership than the row it was copied from. The
/// action name and the actor are the whole point of the entry; the value never was.
/// <see cref="SetSecret"/> exists so a handler cannot get this wrong by reaching for
/// <see cref="Set"/> out of habit.
/// </para>
/// <para>
/// The names are declared here rather than shared with the Audit module because a module
/// may not reference another module (§3 rule 2) — this is the fifth module to do so, after
/// Identity, Directory, Helpdesk and Assets. They are stable strings stored in a text
/// column: add rather than rename. The convention every module follows is
/// <c>&lt;module&gt;.&lt;entity&gt;_&lt;past-tense verb&gt;</c>, all lower snake case.
/// </para>
/// </remarks>
internal static class MonitoringAudit
{
    /// <summary>A device was registered over an asset.</summary>
    public const string DeviceRegistered = "monitoring.device_registered";

    /// <summary>A device's address or polling settings were corrected.</summary>
    public const string DeviceUpdated = "monitoring.device_updated";

    /// <summary>A device was put under the poller's watch.</summary>
    public const string DeviceMonitoringEnabled = "monitoring.device_monitoring_enabled";

    /// <summary>A device was taken off the poller's watch.</summary>
    public const string DeviceMonitoringDisabled = "monitoring.device_monitoring_disabled";

    /// <summary>A device's read-only SNMP community string was set or replaced.</summary>
    public const string DeviceSnmpCredentialSet = "monitoring.device_snmp_credential_set";

    /// <summary>A device's read-only SNMP community string was removed.</summary>
    public const string DeviceSnmpCredentialCleared = "monitoring.device_snmp_credential_cleared";

    /// <summary>The entity type of a device entry.</summary>
    public const string DeviceEntityType = "MonitoredDevice";

    /// <summary>What a secret's diff records instead of the secret.</summary>
    public const string SecretMarker = "(set)";

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
    /// Records that a secret was set, without recording the secret.
    /// </summary>
    /// <remarks>
    /// It takes no value, which is the point: there is no parameter for a handler to pass
    /// the community string into, so the plaintext cannot reach the trail by accident. The
    /// entry says a credential is now configured and nothing more.
    /// </remarks>
    /// <param name="changes">The diff being built.</param>
    /// <param name="field">The field name, camel-cased as the client sees it.</param>
    /// <returns>The diff, for chaining.</returns>
    public static Dictionary<string, AuditFieldChange> SetSecret(
        this Dictionary<string, AuditFieldChange> changes,
        string field)
    {
        ArgumentNullException.ThrowIfNull(changes);
        changes[field] = new AuditFieldChange(null, SecretMarker);
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
