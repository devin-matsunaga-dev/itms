namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// The two priority names a priority change moved between, as they read at the time.
/// </summary>
/// <remarks>
/// A ticket holds its priority as an id, and the name lives on another row — so unlike
/// status, assignment, and resolution, this one value cannot be read off the ticket.
/// <c>TicketHistoryRecorder</c> looks both names up, and only when the ids actually
/// differ, then hands them to <see cref="TicketChanges.Between"/> so that function stays
/// pure.
/// </remarks>
/// <param name="From">The name of the priority the ticket carried before.</param>
/// <param name="To">The name of the priority it carries now.</param>
public readonly record struct TicketPriorityNames(string From, string To);
