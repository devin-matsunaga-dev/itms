using Microsoft.AspNetCore.Mvc;

namespace Itms.Modules.Helpdesk.Features.Tickets.TicketCounters;

/// <summary>The one thing the counters endpoint has to be told.</summary>
public sealed class TicketCountersQuery
{
    /// <summary>
    /// The end of the caller's day, as an instant. Everything open and due before it is
    /// counted as due today.
    /// </summary>
    /// <remarks>
    /// The caller supplies it because the day boundary somebody means is the one on their
    /// own clock, and the wire is UTC (ARCHITECTURE.md §11) — the same call WP-1.9 made for
    /// the created-date range. When it is absent the server falls back to the end of its
    /// own UTC day, which is right for a service account and wrong for a person; a screen
    /// should always send it.
    /// </remarks>
    [FromQuery(Name = "dueBefore")]
    public DateTimeOffset? DueBefore { get; init; }
}
