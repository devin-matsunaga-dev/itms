using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.Tickets;

/// <summary>What a ticket looks like immediately after a status change.</summary>
/// <remarks>
/// Deliberately not the whole ticket. WP-1.5 owns the detail representation; this says
/// only what the transition did, which is what a client that just made one needs in order
/// to update the header and the buttons without a second round trip.
/// </remarks>
/// <param name="Id">The ticket.</param>
/// <param name="Number">Its human-readable number, so a toast can name it.</param>
/// <param name="PreviousStatus">The status it moved from.</param>
/// <param name="Status">The status it is in now.</param>
/// <param name="ChangedAt">When the move happened, in UTC.</param>
/// <param name="ResolvedAt">When it was resolved, or <see langword="null"/> — cleared by a reopen.</param>
/// <param name="ClosedAt">When it was closed, or <see langword="null"/>.</param>
/// <param name="AllowedNextStatuses">
/// Where it may go next, straight from the state machine. WP-1.10 must not render a
/// transition button the server would refuse, and reading it from here is what stops the
/// table being written a second time in TypeScript. Empty from a terminal state.
/// </param>
public sealed record TicketStatusChangeResponse(
    Guid Id,
    string Number,
    TicketStatus PreviousStatus,
    TicketStatus Status,
    DateTimeOffset ChangedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyCollection<TicketStatus> AllowedNextStatuses);

/// <summary>A status change together with the row version the ticket carries after it.</summary>
/// <remarks>
/// Internal and never serialised, for the reason <see cref="TicketDetail"/> gives: the
/// version is a fact about the write rather than part of the ticket, and it travels as an
/// <c>ETag</c> header.
/// </remarks>
/// <param name="Response">The transition as the client sees it.</param>
/// <param name="Version">The <c>xmin</c> row version after the write.</param>
internal sealed record TicketStatusChange(TicketStatusChangeResponse Response, uint Version);
