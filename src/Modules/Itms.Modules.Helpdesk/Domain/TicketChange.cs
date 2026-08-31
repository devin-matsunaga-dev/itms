namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// One movement a change to a ticket produced: which dimension moved, and the two
/// display values it moved between.
/// </summary>
/// <remarks>
/// This is the shape before it becomes a row. <c>TicketHistoryRecorder</c> is what turns
/// it into a <see cref="TicketHistoryEntry"/> with an actor and an instant attached; the
/// separation is what lets <see cref="TicketChanges.Between"/> be a pure function the
/// unit suite can exhaust without a database.
/// </remarks>
/// <param name="Kind">Which dimension moved.</param>
/// <param name="From">
/// What it read before, or <see langword="null"/> when there was nothing there — an
/// unassigned ticket, or one with no resolution recorded yet.
/// </param>
/// <param name="To">What it reads now, or <see langword="null"/> when the change cleared it.</param>
public readonly record struct TicketChange(TicketChangeKind Kind, string? From, string? To);
