namespace Itms.Modules.Assets.Domain;

/// <summary>
/// One line of an asset's timeline: which dimension moved, between which two values, at
/// whose hand, when, and why (invariant 5, SPEC.md §3 "asset history").
/// </summary>
/// <remarks>
/// <para>
/// <b>Write-once, like an audit row, but not an audit row.</b> There is a factory and
/// there are no mutators, because a timeline that can be edited is not a history. It is
/// nevertheless a different thing from the audit trail: the audit trail is the
/// cross-cutting compliance record ARCHITECTURE.md §8 defines, spanning every module and
/// visible to administrators; this is the asset's own narrative, owned by Assets and shown
/// to whoever is looking the equipment up. The two overlap on assignment and status, and
/// will keep overlapping. Invariant 10's append-only guarantee is the audit table's; this
/// table is merely written that way because nothing has any business rewriting it.
/// </para>
/// <para>
/// <b>It is also the half of the record that survives the move to the outbox.</b> The
/// audit rows for these two events are derived from <c>AssetAssigned</c> and
/// <c>AssetStatusChanged</c> by the Audit module's consumer, which runs on a background
/// scope with no principal — so they carry no source IP and no actor name. This table is
/// written inside the request, by the handler, and keeps both. If somebody has to answer
/// "who took this laptop off Alice", it is answered here.
/// </para>
/// <para>
/// <b>The values are point-in-time text, and deliberately not ids.</b> A history entry says
/// what somebody saw when the change happened. If it stored a status id, renaming that
/// status would silently rewrite what every old entry claims — the exact opposite of the
/// by-id resolution <c>AssetResponse</c> uses for the live asset, and for the same reason
/// it uses it: a live asset should follow a rename and a record of the past should not.
/// This is the call WP-1.4 made for a ticket's timeline and WP-0.7 for the audit row's
/// cached actor name.
/// </para>
/// </remarks>
public sealed class AssetHistoryEntry
{
    /// <summary>
    /// The longest a from- or to-value may be. Sized to the cached holder name, which is
    /// the longest thing that lands here — a status name is far shorter.
    /// </summary>
    public const int ValueMaxLength = Asset.AssignedToUserNameMaxLength;

    /// <summary>The longest an actor's cached display name may be.</summary>
    public const int ActorNameMaxLength = Asset.AssignedToUserNameMaxLength;

    /// <summary>The longest the operator's note may be.</summary>
    public const int NoteMaxLength = 1000;

    private AssetHistoryEntry()
    {
        // EF Core materialisation.
    }

    /// <summary>The entry's id. Version 7, so the index on it is time-ordered like the rows.</summary>
    public Guid Id { get; private set; }

    /// <summary>The asset this line belongs to. A real intra-module foreign key.</summary>
    public Guid AssetId { get; private set; }

    /// <summary>Which dimension moved.</summary>
    public AssetChangeKind Kind { get; private set; }

    /// <summary>What it read before, or <see langword="null"/> when there was nothing there.</summary>
    public string? FromValue { get; private set; }

    /// <summary>What it reads now, or <see langword="null"/> when the change cleared it.</summary>
    public string? ToValue { get; private set; }

    /// <summary>
    /// What the operator said about it, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// SPEC.md §3 asks for a note on every asset-history event, which a ticket's timeline
    /// has no equivalent of — a ticket carries its narrative in comments and a resolution,
    /// and an asset has neither. It is free text and it is optional: requiring one would
    /// make booking a box of equipment in and out a typing exercise, and an operator with
    /// nothing to add would type a full stop.
    /// <para>
    /// <b>The same note is written onto every entry one operation produces</b>, rather than
    /// onto the first. The entries are read individually and a paged timeline can put two
    /// entries of one operation on either side of a page boundary, so a note attached to
    /// only one of them would vanish from the page carrying the other.
    /// </para>
    /// </remarks>
    public string? Note { get; private set; }

    /// <summary>When the change happened (UTC). The same instant the change itself carries.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>
    /// Where this line sat among the lines the same operation wrote, counting from zero.
    /// </summary>
    /// <remarks>
    /// One operation can write more than one line — issuing equipment out of stock moves
    /// the holder and the lifecycle status — and those lines share
    /// <see cref="OccurredAt"/> because they genuinely happened at one instant. Without
    /// this, the only tiebreaker available is the id, and a version 7 id is time-ordered
    /// between milliseconds but random within one: the two lines would come back in either
    /// order, and a timeline whose order flips between two reads of the same data is the
    /// kind of bug nobody can reproduce. It is an ordinal within an operation, not a global
    /// sequence — it restarts at zero for the next one.
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
    /// <param name="assetId">The asset the line belongs to.</param>
    /// <param name="change">Which dimension moved, and between which values.</param>
    /// <param name="sequence">Where this line sits among the lines the same operation wrote.</param>
    /// <param name="occurredAt">When the change happened (UTC), from <c>IClock</c>.</param>
    /// <param name="note">What the operator said about it, or <see langword="null"/>.</param>
    /// <param name="actorId">Who made it, or <see langword="null"/> for the system.</param>
    /// <param name="actorName">Their display name, or <see langword="null"/>.</param>
    /// <returns>The new entry, not yet persisted.</returns>
    /// <exception cref="ArgumentException"><paramref name="assetId"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sequence"/> is negative.</exception>
    public static AssetHistoryEntry Record(
        Guid assetId,
        AssetChange change,
        int sequence,
        DateTimeOffset occurredAt,
        string? note,
        Guid? actorId,
        string? actorName)
    {
        if (assetId == Guid.Empty)
        {
            throw new ArgumentException("A history entry must belong to an asset.", nameof(assetId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(sequence);

        return new AssetHistoryEntry
        {
            Id = Guid.CreateVersion7(),
            AssetId = assetId,
            Kind = change.Kind,
            FromValue = Truncate(change.From, ValueMaxLength),
            ToValue = Truncate(change.To, ValueMaxLength),
            Note = Truncate(note, NoteMaxLength),
            OccurredAt = occurredAt,
            Sequence = sequence,
            ActorId = actorId,
            ActorName = Truncate(actorName, ActorNameMaxLength),
        };
    }

    /// <summary>
    /// Caps text at the column's limit, following the ticket timeline's rule and the audit
    /// row's before it: an over-long value must never be able to stop the change it
    /// describes from being recorded at all.
    /// </summary>
    /// <param name="value">The text to cap, or <see langword="null"/>.</param>
    /// <param name="maxLength">The column's limit.</param>
    /// <returns>The text, shortened if it was over the limit.</returns>
    private static string? Truncate(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
