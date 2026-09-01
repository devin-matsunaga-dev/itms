using System.Text.Json.Serialization;

namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// Which dimension of a ticket a history entry records having moved.
/// </summary>
/// <remarks>
/// <para>
/// These are the changes WP-1.4 requires a history entry for — status, priority,
/// assignment, and resolution — plus the two later packages added against the same test:
/// the hold reason (WP-1.13) and the asset the ticket concerns (WP-2.5). They are not a
/// general "anything that changed" list: a subject correction or a description edit is an
/// audit concern, not a line in the timeline a technician reads to understand what
/// happened to a ticket.
/// </para>
/// <para>
/// Stored as text and serialised as text, following <see cref="TicketStatus"/> and
/// <c>LocationKind</c>: a history row is read at a psql prompt during an incident far
/// more often than an enum is renumbered, and the converter on the type is what makes
/// the generated client see a string union rather than an integer.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<TicketChangeKind>))]
public enum TicketChangeKind
{
    /// <summary>The ticket moved through the state machine.</summary>
    Status,

    /// <summary>Its priority was changed.</summary>
    Priority,

    /// <summary>It was assigned, reassigned, or unassigned.</summary>
    Assignment,

    /// <summary>Its resolution notes were recorded, replaced, or cleared.</summary>
    Resolution,

    /// <summary>
    /// It was put on hold with a reason, or that reason was lifted when it resumed.
    /// </summary>
    /// <remarks>
    /// Written at the same instant as the <see cref="Status"/> entry the same move
    /// produces, exactly as <see cref="Resolution"/> is — so a screen that groups entries
    /// sharing an instant renders "on hold, because X" as one event rather than two rows.
    /// </remarks>
    Hold,

    /// <summary>
    /// The asset the ticket concerns was named, changed, or cleared.
    /// </summary>
    /// <remarks>
    /// Recorded as the asset's <em>tag</em> rather than its id, because the timeline is
    /// read by people — often at a psql prompt during an incident — and a bare uuid says
    /// nothing. The tag is resolved through <c>IAssetLookup</c> when the entry is written
    /// and then never refreshed: the line says which asset the ticket was linked to at the
    /// time, which is what a history is for.
    /// </remarks>
    Asset,
}
