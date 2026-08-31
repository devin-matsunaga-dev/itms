namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// One line of a ticket's timeline: which dimension moved, between which two values, at
/// whose hand, and when (invariant 3, SPEC.md §2 "full ticket history").
/// </summary>
/// <remarks>
/// <para>
/// <b>Write-once, like an audit row, but not an audit row.</b> There is a factory and
/// there are no mutators, because a timeline that can be edited is not a history. It is
/// nevertheless a different thing from the audit trail: the audit trail is the
/// cross-cutting compliance record ARCHITECTURE.md §8 defines, spanning every module and
/// visible to administrators; this is the ticket's own narrative, owned by Helpdesk and
/// shown to the technician working it. The two overlap on status changes and will keep
/// overlapping. Invariant 10's append-only guarantee is the audit table's; this table is
/// merely written that way because nothing has any business rewriting it.
/// </para>
/// <para>
/// <b>The values are point-in-time text, and deliberately not ids.</b> A history entry
/// says what somebody saw when the change happened. If it stored a priority id, renaming
/// that priority would silently rewrite what every old entry claims the technician chose —
/// the exact opposite of the by-id propagation WP-1.1 chose for live tickets, and for the
/// same reason it chose it: a live ticket should follow a rename and a record of the past
/// should not. This is the call WP-0.7 already made for the audit row's cached
/// <c>actor_name</c>. Relational analysis over history — "how many tickets were ever
/// Critical" — is not a V1 requirement and would need id columns added deliberately, not
/// a text column reinterpreted.
/// </para>
/// </remarks>
public sealed class TicketHistoryEntry
{
    /// <summary>
    /// The longest a from- or to-value may be. Sized to the resolution notes, which are
    /// far and away the longest thing that lands here.
    /// </summary>
    public const int ValueMaxLength = Ticket.ResolutionNotesMaxLength;

    /// <summary>The longest an actor's cached display name may be.</summary>
    public const int ActorNameMaxLength = Ticket.DisplayNameMaxLength;

    private TicketHistoryEntry()
    {
        // EF Core materialisation.
    }

    /// <summary>The entry's id. Version 7, so the index on it is time-ordered like the rows.</summary>
    public Guid Id { get; private set; }

    /// <summary>The ticket this line belongs to. A real intra-module foreign key.</summary>
    public Guid TicketId { get; private set; }

    /// <summary>Which dimension moved.</summary>
    public TicketChangeKind Kind { get; private set; }

    /// <summary>What it read before, or <see langword="null"/> when there was nothing there.</summary>
    public string? FromValue { get; private set; }

    /// <summary>What it reads now, or <see langword="null"/> when the change cleared it.</summary>
    public string? ToValue { get; private set; }

    /// <summary>When the change happened (UTC). The same instant the change itself carries.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>
    /// Where this line sat among the lines the same change wrote, counting from zero.
    /// </summary>
    /// <remarks>
    /// One change can write more than one line — resolving moves the status and records
    /// what was done — and those lines share <see cref="OccurredAt"/> because they genuinely
    /// happened at one instant. Without this, the only tiebreaker available is the id, and
    /// a version 7 id is time-ordered between milliseconds but random within one: the two
    /// lines would come back in either order, and a timeline whose order flips between two
    /// reads of the same data is the kind of bug nobody can reproduce. It is an ordinal
    /// within a change, not a global sequence — it restarts at zero for the next one.
    /// </remarks>
    public int Sequence { get; private set; }

    /// <summary>Who made it, or <see langword="null"/> when the system did.</summary>
    public Guid? ActorId { get; private set; }

    /// <summary>
    /// Their display name as it was at the time, or <see langword="null"/>. Cached rather
    /// than looked up, per §3 rule 6 and for the same reason the values are: the timeline
    /// has to stay readable after the account is renamed or deactivated.
    /// </summary>
    public string? ActorName { get; private set; }

    /// <summary>Writes one line of the timeline. The only way an entry comes into existence.</summary>
    /// <param name="ticketId">The ticket the line belongs to.</param>
    /// <param name="change">Which dimension moved, and between which values.</param>
    /// <param name="sequence">Where this line sits among the lines the same change wrote.</param>
    /// <param name="occurredAt">When the change happened (UTC), from <c>IClock</c>.</param>
    /// <param name="actorId">Who made it, or <see langword="null"/> for the system.</param>
    /// <param name="actorName">Their display name, or <see langword="null"/>.</param>
    /// <returns>The new entry, not yet persisted.</returns>
    /// <exception cref="ArgumentException"><paramref name="ticketId"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sequence"/> is negative.</exception>
    public static TicketHistoryEntry Record(
        Guid ticketId,
        TicketChange change,
        int sequence,
        DateTimeOffset occurredAt,
        Guid? actorId,
        string? actorName)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A history entry must belong to a ticket.", nameof(ticketId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(sequence);

        return new TicketHistoryEntry
        {
            Id = Guid.CreateVersion7(),
            TicketId = ticketId,
            Kind = change.Kind,
            FromValue = Truncate(change.From, ValueMaxLength),
            ToValue = Truncate(change.To, ValueMaxLength),
            OccurredAt = occurredAt,
            Sequence = sequence,
            ActorId = actorId,
            ActorName = Truncate(actorName, ActorNameMaxLength),
        };
    }

    /// <summary>
    /// Caps text at the column's limit, following the audit row's rule: an over-long value
    /// must never be able to stop the change it describes from being recorded at all.
    /// </summary>
    /// <param name="value">The text to cap, or <see langword="null"/>.</param>
    /// <param name="maxLength">The column's limit.</param>
    /// <returns>The text, shortened if it was over the limit.</returns>
    private static string? Truncate(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
