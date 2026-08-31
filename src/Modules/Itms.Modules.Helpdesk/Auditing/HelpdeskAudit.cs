using Itms.Contracts.Auditing;

namespace Itms.Modules.Helpdesk.Auditing;

/// <summary>
/// The action identifiers this module writes through <c>IAuditWriter</c>, and the small
/// helpers its handlers build a diff with.
/// </summary>
/// <remarks>
/// <para>
/// Categories and priorities change through plain handlers and raise no domain event —
/// ARCHITECTURE.md §5 names none and nothing consumes one — so they are exactly the
/// "mutations that do not warrant a domain event" §8 keeps <c>IAuditWriter</c> for.
/// SPEC.md §15 counts them as administrative configuration changes, which are mandatory
/// coverage.
/// </para>
/// <para>
/// The names are declared here rather than shared with the Audit module because a module
/// may not reference another module (§3 rule 2). They are stable strings stored in a text
/// column: add rather than rename. The convention every module follows is
/// <c>&lt;module&gt;.&lt;entity&gt;_&lt;past-tense verb&gt;</c>, all lower snake case.
/// </para>
/// </remarks>
internal static class HelpdeskAudit
{
    /// <summary>A ticket category was created.</summary>
    public const string CategoryCreated = "helpdesk.category_created";

    /// <summary>A ticket category's name, description, or order changed.</summary>
    public const string CategoryUpdated = "helpdesk.category_updated";

    /// <summary>A ticket category was retired.</summary>
    public const string CategoryRetired = "helpdesk.category_retired";

    /// <summary>A retired ticket category was brought back.</summary>
    public const string CategoryReinstated = "helpdesk.category_reinstated";

    /// <summary>A ticket priority was created.</summary>
    public const string PriorityCreated = "helpdesk.priority_created";

    /// <summary>A ticket priority's name, description, order, or SLA targets changed.</summary>
    public const string PriorityUpdated = "helpdesk.priority_updated";

    /// <summary>A ticket priority was retired.</summary>
    public const string PriorityRetired = "helpdesk.priority_retired";

    /// <summary>A retired ticket priority was brought back.</summary>
    public const string PriorityReinstated = "helpdesk.priority_reinstated";

    // There is deliberately no ticket action here. WP-1.3 wrote
    // "helpdesk.ticket_status_changed" through IAuditWriter because it could not publish —
    // IEventPublisher was still inside the bus — and left a warning that whichever package
    // started publishing TicketStatusChanged had to delete the writer call. WP-1.6 did:
    // every ticket action in the trail is now derived from a domain event by the Audit
    // module, under ticket.created, ticket.assigned, ticket.status_changed, and
    // ticket.resolved. A module that adds a ticket action here again is reintroducing the
    // double-row trap, not filling a gap.

    /// <summary>The entity type of a category entry.</summary>
    public const string CategoryEntityType = "TicketCategory";

    /// <summary>The entity type of a priority entry.</summary>
    public const string PriorityEntityType = "TicketPriority";

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
