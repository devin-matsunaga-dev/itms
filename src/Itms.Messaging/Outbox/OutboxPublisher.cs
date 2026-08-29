using Itms.Contracts.Events;
using Itms.Messaging.Abstractions;
using Itms.Messaging.Serialization;
using Itms.Platform.Time;
using Microsoft.Extensions.Logging;

namespace Itms.Messaging.Outbox;

/// <summary>
/// Writes published events into the outbox on the caller's connection, inside the
/// caller's transaction.
/// </summary>
/// <param name="session">The scope's shared connection and ambient transaction.</param>
/// <param name="context">The outbox context, built on that same connection.</param>
/// <param name="serializer">Turns the event into a stored payload.</param>
/// <param name="clock">The system's only source of "now".</param>
/// <param name="logger">Structured log sink.</param>
public sealed class OutboxPublisher(
    IDbSession session,
    OutboxDbContext context,
    DomainEventSerializer serializer,
    IClock clock,
    ILogger<OutboxPublisher> logger) : IEventPublisher
{
    private readonly IDbSession _session = session;
    private readonly OutboxDbContext _context = context;
    private readonly DomainEventSerializer _serializer = serializer;
    private readonly IClock _clock = clock;
    private readonly ILogger<OutboxPublisher> _logger = logger;

    /// <inheritdoc />
    public Task PublishAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return PublishAsync([domainEvent], cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishAsync(IEnumerable<DomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        var events = domainEvents as IReadOnlyCollection<DomainEvent> ?? [.. domainEvents];
        if (events.Count == 0)
        {
            return;
        }

        // The whole point of the outbox is that the event and the change it announces
        // share a transaction. Publishing without one would silently reintroduce the
        // failure mode the outbox exists to remove, so it is refused rather than tolerated.
        if (_session.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Events must be published inside a transaction. Wrap the consumer's work in IDbSession.ExecuteInTransactionAsync so the state change and its events commit together.");
        }

        await _session.EnlistAsync(_context, cancellationToken).ConfigureAwait(false);

        var now = _clock.UtcNow;
        foreach (var domainEvent in events)
        {
            var (eventType, payload) = DomainEventSerializer.Serialize(domainEvent);
            _context.Messages.Add(OutboxMessage.Create(
                domainEvent.EventId,
                eventType,
                payload,
                domainEvent.OccurredAt,
                now));

            _logger.EventStaged(eventType, domainEvent.EventId);
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
