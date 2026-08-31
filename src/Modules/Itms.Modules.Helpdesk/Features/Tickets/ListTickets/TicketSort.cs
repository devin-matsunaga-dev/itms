using System.Text.Json.Serialization;

namespace Itms.Modules.Helpdesk.Features.Tickets.ListTickets;

/// <summary>What the ticket queue is ordered by.</summary>
/// <remarks>
/// <para>
/// A closed set rather than a free-text column name, because a sort that reaches the
/// database as a string is either a table scan on an unindexed column or an injection
/// question nobody wants to have to answer. An unrecognised value is a 400 from model
/// binding, not a silent fallback.
/// </para>
/// <para>
/// <b>The default is <see cref="CreatedAt"/> descending</b> — newest first, at the human's
/// direction. It is the neutral, deterministic answer; the queue ordering a technician
/// actually wants (priority, then age) is a view WP-1.9 asks for explicitly with
/// <see cref="Priority"/>, rather than something the API decides on their behalf.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<TicketSort>))]
public enum TicketSort
{
    /// <summary>When the ticket was raised. The default.</summary>
    CreatedAt,

    /// <summary>When the ticket last moved.</summary>
    UpdatedAt,

    /// <summary>
    /// The priority's rank, so the most urgent leads. Ties break by age, oldest first,
    /// which is the queue order WP-1.9's technician view wants.
    /// </summary>
    Priority,

    /// <summary>The ticket number, which is also creation order — but reads as a number to a person.</summary>
    Number,

    /// <summary>
    /// When resolution is due — the soonest first by default, because that is the end of
    /// the queue somebody is triaging. Pauses are already folded into the column.
    /// </summary>
    DueAt,
}
