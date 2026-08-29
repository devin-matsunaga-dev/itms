namespace Itms.Messaging.Outbox;

/// <summary>
/// One published domain event, durable in the same transaction as the change it
/// announces. This row <em>is</em> the delivery guarantee (ARCHITECTURE.md §5): there
/// is no broker behind it, so nothing about a message may live only in memory.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage(
        Guid id,
        string eventType,
        string payload,
        DateTimeOffset occurredAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        EventType = eventType;
        Payload = payload;
        OccurredAt = occurredAt;
        CreatedAt = createdAt;
        AvailableAt = createdAt;
    }

    /// <summary>EF Core materialisation constructor.</summary>
    private OutboxMessage()
    {
        EventType = null!;
        Payload = null!;
    }

    /// <summary>
    /// The event id, reused as the primary key. It is a v7 GUID, so the key is
    /// time-ordered and the index does not fragment as the table grows.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>The CLR type name the payload deserialises to, resolved through the event type registry.</summary>
    public string EventType { get; private set; }

    /// <summary>The serialised event, as JSON.</summary>
    public string Payload { get; private set; }

    /// <summary>When the fact became true, per the publishing consumer.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>When the row was written.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// The earliest instant a dispatcher may claim this message. Claiming pushes it
    /// forward by the lease duration, which is what stops two dispatcher instances —
    /// or a restarted one — from working the same message at the same time.
    /// </summary>
    public DateTimeOffset AvailableAt { get; private set; }

    /// <summary>How many times a dispatcher has claimed this message.</summary>
    public int Attempts { get; private set; }

    /// <summary>When every consumer had consumed it, or <see langword="null"/> while it is outstanding.</summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>When it exhausted its attempts and was parked, or <see langword="null"/>.</summary>
    public DateTimeOffset? FailedAt { get; private set; }

    /// <summary>The most recent failure, for diagnosis. Never contains a payload or a stack trace.</summary>
    public string? LastError { get; private set; }

    /// <summary>Creates a message for an event that has just been published.</summary>
    /// <param name="id">The event id.</param>
    /// <param name="eventType">The resolvable type name.</param>
    /// <param name="payload">The serialised event.</param>
    /// <param name="occurredAt">When the fact became true.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <returns>The new message, immediately available for dispatch.</returns>
    public static OutboxMessage Create(
        Guid id,
        string eventType,
        string payload,
        DateTimeOffset occurredAt,
        DateTimeOffset now) =>
        new(id, eventType, payload, occurredAt, now);

    /// <summary>Records a successful pass in which every consumer consumed the message.</summary>
    /// <param name="now">The current instant.</param>
    public void MarkProcessed(DateTimeOffset now)
    {
        ProcessedAt = now;
        LastError = null;
    }

    /// <summary>Schedules another attempt after <paramref name="retryAt"/>.</summary>
    /// <param name="retryAt">When the message becomes claimable again.</param>
    /// <param name="error">A short description of what failed.</param>
    public void MarkForRetry(DateTimeOffset retryAt, string error)
    {
        AvailableAt = retryAt;
        LastError = Truncate(error);
    }

    /// <summary>
    /// Parks the message after its attempts are exhausted. Nothing deletes it: a
    /// dead-lettered event is evidence, and reviving one is a deliberate operator act.
    /// </summary>
    /// <param name="now">The current instant.</param>
    /// <param name="error">A short description of the final failure.</param>
    public void MarkFailed(DateTimeOffset now, string error)
    {
        FailedAt = now;
        LastError = Truncate(error);
    }

    /// <summary>Records that a dispatcher has taken the message, leasing it until <paramref name="leaseUntil"/>.</summary>
    /// <param name="leaseUntil">When the lease lapses and another dispatcher may claim it.</param>
    public void Claim(DateTimeOffset leaseUntil)
    {
        Attempts++;
        AvailableAt = leaseUntil;
    }

    // The column is capped so a pathological exception message cannot bloat the table.
    private static string Truncate(string error) =>
        error.Length <= 2000 ? error : error[..2000];
}
