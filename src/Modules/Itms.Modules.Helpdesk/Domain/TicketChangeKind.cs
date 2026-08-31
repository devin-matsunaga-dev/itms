using System.Text.Json.Serialization;

namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// Which dimension of a ticket a history entry records having moved.
/// </summary>
/// <remarks>
/// <para>
/// These are exactly the four changes WP-1.4 requires a history entry for — status,
/// priority, assignment, and resolution. They are not a general "anything that changed"
/// list: a subject correction or a description edit is an audit concern, not a line in
/// the timeline a technician reads to understand what happened to a ticket.
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
}
