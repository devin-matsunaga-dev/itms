namespace Itms.Contracts.Events;

/// <summary>
/// The base of every domain event. Events are past-tense facts a module publishes
/// about its own data (ARCHITECTURE.md §5); other modules react to them instead of
/// calling each other's handlers, which is what keeps the module boundaries real.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EventId"/> is the idempotency key. The outbox guarantees at-least-once
/// delivery, so a consumer that is not keyed on this id will eventually double its
/// side effect — a second notification, a second audit row.
/// </para>
/// <para>
/// The event is set on construction and never mutated. WP-0.4 owns publishing;
/// this type carries no behaviour of its own.
/// </para>
/// </remarks>
public abstract record DomainEvent
{
    /// <summary>The unique id of this event occurrence. Consumers deduplicate on it.</summary>
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// When the fact became true, in UTC. Set by the publishing handler from
    /// <c>IClock</c> where the exact instant matters; otherwise at construction.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The user whose action produced the event, or <see langword="null"/> when the
    /// system itself did — a poller-driven device transition has no human actor.
    /// The audit trail records this as the actor.
    /// </summary>
    public Guid? ActorId { get; init; }
}
