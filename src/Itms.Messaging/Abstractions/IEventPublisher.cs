using Itms.Contracts.Events;

namespace Itms.Messaging.Abstractions;

/// <summary>
/// How a module announces a fact about its own data. Modules depend on this instead
/// of on each other (ARCHITECTURE.md §3 rule 3), which is what keeps a Helpdesk
/// consumer from ever calling into Notifications.
/// </summary>
/// <remarks>
/// Publishing writes an outbox row through the caller's own database transaction, so
/// the state change and the event it announces commit or roll back together. There is
/// no in-memory queue and no send-on-commit callback to lose: if the transaction
/// commits, the event is durable, and if it rolls back the event never existed.
/// </remarks>
public interface IEventPublisher
{
    /// <summary>
    /// Stages <paramref name="domainEvent"/> for delivery inside the ambient
    /// transaction opened by <see cref="IDbSession.ExecuteInTransactionAsync"/>.
    /// </summary>
    /// <param name="domainEvent">The past-tense fact being announced.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="InvalidOperationException">
    /// There is no ambient transaction. Publishing outside one would let an event
    /// survive a change that rolled back, so it is a programming error rather than a
    /// failure the caller can act on.
    /// </exception>
    Task PublishAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default);

    /// <summary>Stages several events in one call. Identical semantics to publishing each in turn.</summary>
    /// <param name="domainEvents">The facts being announced, in order.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task PublishAsync(IEnumerable<DomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
