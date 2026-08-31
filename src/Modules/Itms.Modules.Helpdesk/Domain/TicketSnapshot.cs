namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// The four dimensions of a ticket that <see cref="TicketHistoryEntry"/> records, read
/// off the entity at one instant.
/// </summary>
/// <remarks>
/// <para>
/// A handler takes one of these <em>before</em> it calls into the entity and hands it to
/// <c>TicketHistoryRecorder</c> afterwards. That is the whole point of the type: invariant
/// 3 requires a history entry for every meaningful change, and a handler that has to
/// remember <em>which</em> entries its change produces is a handler that will eventually
/// forget one. Capturing the before-state is the only thing a caller has to remember, and
/// forgetting that is not silent — no entries are written at all, which the tests catch.
/// </para>
/// <para>
/// It deliberately holds no subject, description, category, or due date. Those change too,
/// and they are audited; they are not what the timeline is for.
/// </para>
/// </remarks>
/// <param name="Status">Where the ticket sat in the workflow.</param>
/// <param name="PriorityId">
/// Which priority it carried. Held as an id rather than a name because the name is on
/// another row; the recorder resolves both ids to the names they had at the time, and
/// only when they actually differ.
/// </param>
/// <param name="AssigneeId">Who was responsible, or <see langword="null"/> when nobody was.</param>
/// <param name="AssigneeName">Their display name as the ticket cached it, or <see langword="null"/>.</param>
/// <param name="ResolutionNotes">What the ticket recorded as its resolution, or <see langword="null"/>.</param>
public readonly record struct TicketSnapshot(
    TicketStatus Status,
    Guid PriorityId,
    Guid? AssigneeId,
    string? AssigneeName,
    string? ResolutionNotes)
{
    /// <summary>Reads the four tracked dimensions off a ticket as it stands right now.</summary>
    /// <param name="ticket">The ticket to read.</param>
    /// <returns>The snapshot.</returns>
    public static TicketSnapshot Of(Ticket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        return new TicketSnapshot(
            ticket.Status,
            ticket.PriorityId,
            ticket.AssigneeId,
            ticket.AssigneeName,
            ticket.ResolutionNotes);
    }
}
