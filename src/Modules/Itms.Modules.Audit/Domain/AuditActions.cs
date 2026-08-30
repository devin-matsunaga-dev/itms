namespace Itms.Modules.Audit.Domain;

/// <summary>
/// The action identifiers this module writes when it consumes a domain event.
/// </summary>
/// <remarks>
/// They are stable strings, not an enum, because they are stored in a text column and
/// read back by a viewer and by whoever is looking at the table during an incident.
/// Renaming one orphans the history it describes, so add rather than rename.
/// <para>
/// Only the event-derived actions live here. A module that calls <c>IAuditWriter</c>
/// directly names its own actions in its own assembly — it may not reference this one
/// (ARCHITECTURE.md §3) — which is why <c>auth.*</c> and <c>directory.*</c> are absent.
/// </para>
/// </remarks>
public static class AuditActions
{
    /// <summary>A ticket was created.</summary>
    public const string TicketCreated = "ticket.created";

    /// <summary>A ticket's assignee changed.</summary>
    public const string TicketAssigned = "ticket.assigned";

    /// <summary>A ticket moved through the state machine.</summary>
    public const string TicketStatusChanged = "ticket.status_changed";

    /// <summary>A ticket was resolved.</summary>
    public const string TicketResolved = "ticket.resolved";

    /// <summary>An asset was assigned to a user or returned from one.</summary>
    public const string AssetAssigned = "asset.assigned";

    /// <summary>An asset's lifecycle status changed.</summary>
    public const string AssetStatusChanged = "asset.status_changed";

    /// <summary>A monitored device was declared offline.</summary>
    public const string DeviceWentOffline = "device.went_offline";

    /// <summary>A monitored device answered a check again.</summary>
    public const string DeviceRecovered = "device.recovered";

    /// <summary>An alert was raised for a device.</summary>
    public const string AlertRaised = "alert.raised";

    /// <summary>An alert was resolved.</summary>
    public const string AlertResolved = "alert.resolved";

    /// <summary>A user was deactivated.</summary>
    public const string UserDeactivated = "user.deactivated";
}

/// <summary>
/// The entity type names the audit trail uses. One name per kind of thing, chosen to
/// read the way a person would say it.
/// </summary>
public static class AuditEntityTypes
{
    /// <summary>A helpdesk ticket.</summary>
    public const string Ticket = "Ticket";

    /// <summary>An asset record.</summary>
    public const string Asset = "Asset";

    /// <summary>A monitored device.</summary>
    public const string Device = "Device";

    /// <summary>A monitoring alert.</summary>
    public const string Alert = "Alert";

    /// <summary>A user account.</summary>
    public const string User = "User";
}
