using Itms.Contracts.Events;

namespace Itms.Messaging.Abstractions;

/// <summary>
/// Reacts to a domain event published by another module. Implementations are
/// discovered by assembly scan at the composition root, so adding a reaction is
/// adding a class — no registration list to keep in step.
/// </summary>
/// <remarks>
/// <para>
/// Delivery is at-least-once. The dispatcher records a consumption row per
/// (event, consumer) in the same transaction as the consumer's own work, so a consumer
/// whose side effects are in this database is made exactly-once for free. A consumer
/// that reaches outside the database — sending mail, calling an API — is responsible
/// for its own idempotency, keyed on <see cref="DomainEvent.EventId"/>.
/// </para>
/// <para>
/// Throwing is how a consumer asks to be retried. It will be retried with exponential
/// backoff, and only that consumer will: siblings that already succeeded are not re-run.
/// </para>
/// </remarks>
/// <typeparam name="TEvent">The event this consumer reacts to.</typeparam>
public interface IEventConsumer<in TEvent>
    where TEvent : DomainEvent
{
    /// <summary>Reacts to <paramref name="domainEvent"/>.</summary>
    /// <param name="domainEvent">The event being delivered.</param>
    /// <param name="cancellationToken">Cancels the consumer; a cancelled consumer is retried, not failed.</param>
    Task ConsumeAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
