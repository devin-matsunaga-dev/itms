namespace Itms.Modules.Audit.Domain;

/// <summary>
/// One row of the audit trail: who did what, to which entity, when, from where, and
/// which fields moved (ARCHITECTURE.md §8).
/// </summary>
/// <remarks>
/// <para>
/// The type is deliberately write-once. There is a factory and there are no mutators —
/// no setter, no <c>Rename</c>, no <c>Correct</c> — because invariant 10 says an audit
/// entry is never modified or deleted through any code path in this system. The
/// database enforces the same rule with a trigger, so a hand-written <c>UPDATE</c> in
/// psql fails too.
/// </para>
/// <para>
/// Every text field is capped rather than trusted. Most of what lands here is
/// attacker-influenced — a submitted user name, a department description — and a row
/// nobody can bound is a row that can be used to make the trail unreadable.
/// </para>
/// </remarks>
public sealed class AuditRecord
{
    /// <summary>The longest an action identifier may be.</summary>
    public const int ActionMaxLength = 128;

    /// <summary>The longest an entity type name may be.</summary>
    public const int EntityTypeMaxLength = 128;

    /// <summary>
    /// The longest an entity id may be, as text. Wide enough for the longest identifier
    /// a sign-in may submit, because a failed sign-in against an account that does not
    /// exist has no id to record but the string somebody typed.
    /// </summary>
    public const int EntityIdMaxLength = 320;

    /// <summary>The longest an actor's cached display name may be.</summary>
    public const int ActorNameMaxLength = 256;

    /// <summary>The longest a source address may be — an IPv6 address with a scope id fits.</summary>
    public const int SourceIpMaxLength = 64;

    private AuditRecord()
    {
        // EF Core materialisation; all three are non-null in the database.
        Action = null!;
        EntityType = null!;
        EntityId = null!;
    }

    /// <summary>The row's id. Time-ordered, so the index on it does not fragment.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// When the audited thing happened (UTC). For an entry derived from a domain event
    /// this is the event's own instant, not the moment the dispatcher got to it.
    /// </summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Who did it, or <see langword="null"/> when the system itself did.</summary>
    public Guid? ActorId { get; private set; }

    /// <summary>
    /// The actor's display name as it was at the time, or <see langword="null"/>.
    /// Cached rather than looked up, per §3 rule 6: the trail has to stay readable after
    /// the account is renamed or deactivated.
    /// </summary>
    public string? ActorName { get; private set; }

    /// <summary>What happened, as a stable identifier such as <c>ticket.created</c>.</summary>
    public string Action { get; private set; }

    /// <summary>The kind of entity acted on, such as <c>Ticket</c>.</summary>
    public string EntityType { get; private set; }

    /// <summary>The entity's id as text — not every audited entity is keyed by a Guid.</summary>
    public string EntityId { get; private set; }

    /// <summary>
    /// The caller's address, or <see langword="null"/>. Null is normal for an entry
    /// derived from a domain event: the dispatcher runs outside any request and the
    /// event carries no request context.
    /// </summary>
    public string? SourceIp { get; private set; }

    /// <summary>
    /// The changed fields as a JSON object of <c>{ before, after }</c> pairs, or
    /// <see langword="null"/> when the action changed nothing. Stored as <c>jsonb</c> so
    /// the viewer in WP-5.9 can filter on a field name without a second table.
    /// </summary>
    public string? Changes { get; private set; }

    /// <summary>When the row itself was written (UTC), which is not always <see cref="OccurredAt"/>.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Creates an audit row. The only way one comes into existence.</summary>
    /// <param name="occurredAt">When the audited thing happened (UTC).</param>
    /// <param name="actorId">Who did it, or <see langword="null"/> for the system.</param>
    /// <param name="actorName">Their display name at the time, or <see langword="null"/>.</param>
    /// <param name="action">The stable action identifier.</param>
    /// <param name="entityType">The kind of entity acted on.</param>
    /// <param name="entityId">The entity's id, as text.</param>
    /// <param name="sourceIp">The caller's address, or <see langword="null"/>.</param>
    /// <param name="changes">The changed fields as a JSON object, or <see langword="null"/>.</param>
    /// <param name="createdAt">When the row is being written (UTC).</param>
    /// <returns>The new row, not yet persisted.</returns>
    /// <exception cref="ArgumentException"><paramref name="action"/>, <paramref name="entityType"/>, or <paramref name="entityId"/> is blank.</exception>
    public static AuditRecord Create(
        DateTimeOffset occurredAt,
        Guid? actorId,
        string? actorName,
        string action,
        string entityType,
        string entityId,
        string? sourceIp,
        string? changes,
        DateTimeOffset createdAt)
    {
        // An entry that cannot say what happened or to what is worse than no entry: it
        // is a row that looks like coverage and is not.
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        return new AuditRecord
        {
            Id = Guid.CreateVersion7(),
            OccurredAt = occurredAt,
            ActorId = actorId,
            ActorName = Truncate(actorName, ActorNameMaxLength),
            Action = Truncate(action, ActionMaxLength)!,
            EntityType = Truncate(entityType, EntityTypeMaxLength)!,
            EntityId = Truncate(entityId, EntityIdMaxLength)!,
            SourceIp = Truncate(sourceIp, SourceIpMaxLength),
            Changes = changes,
            CreatedAt = createdAt,
        };
    }

    /// <summary>Caps <paramref name="value"/> at <paramref name="maxLength"/>.</summary>
    /// <param name="value">The text to cap, or <see langword="null"/>.</param>
    /// <param name="maxLength">The column's limit.</param>
    /// <returns>The text, shortened if it was over the limit.</returns>
    /// <remarks>
    /// Truncating rather than rejecting: an over-long value must not be able to stop the
    /// action it describes from being recorded at all.
    /// </remarks>
    public static string? Truncate(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
