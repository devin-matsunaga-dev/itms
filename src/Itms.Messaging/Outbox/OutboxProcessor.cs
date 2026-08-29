using Itms.Messaging.Abstractions;
using Itms.Messaging.Serialization;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Itms.Messaging.Outbox;

/// <summary>
/// Claims outstanding outbox messages and delivers each to its consumers.
/// </summary>
/// <remarks>
/// <para>
/// Claiming is a lease, not a lock held across the work: one statement marks a batch
/// invisible for <see cref="MessagingOptions.LeaseDuration"/> and commits immediately.
/// Holding a row lock for the duration of the consumers would mean one slow consumer
/// blocking an entire batch, and a crashed dispatcher blocking it until the connection
/// died.
/// </para>
/// <para>
/// Each consumer then runs in its own transaction alongside the row that records its
/// consumption. That is what makes a partial failure safe: a consumer that succeeded has
/// committed its work and its consumption together, so the retry re-runs only the one
/// that threw.
/// </para>
/// </remarks>
/// <param name="session">The pass's connection and transaction scope.</param>
/// <param name="context">The outbox context, on that same connection.</param>
/// <param name="serializer">Reconstructs events from stored payloads.</param>
/// <param name="consumers">Which consumers react to which event.</param>
/// <param name="scopeServices">Resolves consumer instances for this scope.</param>
/// <param name="clock">The system's only source of "now".</param>
/// <param name="options">Batch size, lease, and backoff settings.</param>
/// <param name="logger">Structured log sink.</param>
public sealed class OutboxProcessor(
    IDbSession session,
    OutboxDbContext context,
    DomainEventSerializer serializer,
    EventConsumerRegistry consumers,
    IServiceProvider scopeServices,
    IClock clock,
    IOptions<MessagingOptions> options,
    ILogger<OutboxProcessor> logger) : IOutboxProcessor
{
    // A data-modifying CTE, so the select-and-lease is one statement and one round trip.
    // SKIP LOCKED is what lets a second dispatcher instance work the same table without
    // either of them waiting on the other.
    private const string ClaimSql = """
        WITH claimed AS (
            SELECT id
            FROM messaging.outbox_messages
            WHERE processed_at IS NULL
              AND failed_at IS NULL
              AND available_at <= @now
            ORDER BY available_at, id
            LIMIT @batch_size
            FOR UPDATE SKIP LOCKED
        )
        UPDATE messaging.outbox_messages AS m
        SET attempts = m.attempts + 1,
            available_at = @lease_until
        FROM claimed AS c
        WHERE m.id = c.id
        RETURNING m.id, m.event_type, m.payload, m.attempts;
        """;

    private readonly IDbSession _session = session;
    private readonly OutboxDbContext _context = context;
    private readonly DomainEventSerializer _serializer = serializer;
    private readonly EventConsumerRegistry _consumers = consumers;
    private readonly IServiceProvider _scopeServices = scopeServices;
    private readonly IClock _clock = clock;
    private readonly MessagingOptions _options = options.Value;
    private readonly ILogger<OutboxProcessor> _logger = logger;

    /// <inheritdoc />
    public async Task<int> ProcessOnceAsync(CancellationToken cancellationToken = default)
    {
        var claimed = await ClaimAsync(cancellationToken).ConfigureAwait(false);
        if (claimed.Count == 0)
        {
            return 0;
        }

        foreach (var message in claimed)
        {
            await DispatchAsync(message, cancellationToken).ConfigureAwait(false);
        }

        return claimed.Count;
    }

    private async Task<IReadOnlyList<ClaimedMessage>> ClaimAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var connection = (NpgsqlConnection)await _session.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new NpgsqlCommand(ClaimSql, connection);
        command.Parameters.Add(new NpgsqlParameter<DateTimeOffset>("now", NpgsqlDbType.TimestampTz) { TypedValue = now });
        command.Parameters.Add(new NpgsqlParameter<DateTimeOffset>("lease_until", NpgsqlDbType.TimestampTz) { TypedValue = now + _options.LeaseDuration });
        command.Parameters.Add(new NpgsqlParameter<int>("batch_size", NpgsqlDbType.Integer) { TypedValue = _options.BatchSize });

        var claimed = new List<ClaimedMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            claimed.Add(new ClaimedMessage(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3)));
        }

        return claimed;
    }

    private async Task DispatchAsync(ClaimedMessage message, CancellationToken cancellationToken)
    {
        var domainEvent = _serializer.Deserialize(message.EventType, message.Payload);
        if (domainEvent is null)
        {
            // A message written by a newer build than this one. Leave it for a dispatcher
            // that understands it rather than dead-lettering a perfectly good event.
            _logger.UnknownEventType(message.Id, message.EventType);

            await ReleaseAsync(message, "Unknown event type for this build.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var registrations = _consumers.For(domainEvent.GetType());
        if (registrations.Count == 0)
        {
            // Nothing reacts to this event yet. That is normal — Phase 0 publishes events
            // that Phase 1 consumers do not exist for — and it is completion, not failure.
            await CompleteAsync(message.Id, cancellationToken).ConfigureAwait(false);
            return;
        }

        var consumed = await _context.Consumptions
            .AsNoTracking()
            .Where(c => c.MessageId == message.Id)
            .Select(c => c.ConsumerName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var alreadyConsumed = consumed.ToHashSet(StringComparer.Ordinal);
        string? firstFailure = null;

        foreach (var registration in registrations)
        {
            if (alreadyConsumed.Contains(registration.Name))
            {
                continue;
            }

            var failure = await InvokeConsumerAsync(registration, domainEvent, message.Id, cancellationToken)
                .ConfigureAwait(false);

            firstFailure ??= failure;
        }

        if (firstFailure is null)
        {
            await CompleteAsync(message.Id, cancellationToken).ConfigureAwait(false);
            return;
        }

        await ScheduleRetryAsync(message, firstFailure, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> InvokeConsumerAsync(
        EventConsumerRegistration registration,
        Contracts.Events.DomainEvent domainEvent,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _session.ExecuteInTransactionAsync(
                async ct =>
                {
                    var consumer = _scopeServices.GetRequiredService(registration.ConsumerType);
                    await registration.InvokeAsync(consumer, domainEvent, ct).ConfigureAwait(false);

                    // Same transaction as the consumer's own work. This is the exactly-once
                    // guarantee: the effect and the record of it commit or roll back together.
                    await _session.EnlistAsync(_context, ct).ConfigureAwait(false);
                    _context.Consumptions.Add(OutboxConsumption.Create(messageId, registration.Name, _clock.UtcNow));
                    await _context.SaveChangesAsync(ct).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);

            _logger.ConsumerSucceeded(registration.Name, messageId);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A throwing consumer is how a consumer asks to be retried, so this is logged
            // and turned into a backoff rather than being allowed to stop the batch.
            _logger.ConsumerFailed(exception, registration.Name, messageId);

            // The change tracker still holds the consumption that rolled back. Left in
            // place it would be re-sent on the next SaveChanges in this scope, quietly
            // marking a consumer that never succeeded as done.
            _context.ChangeTracker.Clear();

            return $"{registration.Name}: {exception.GetType().Name}: {exception.Message}";
        }
    }

    private async Task CompleteAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        await _context.Messages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(m => m.ProcessedAt, now).SetProperty(m => m.LastError, (string?)null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ScheduleRetryAsync(ClaimedMessage message, string error, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var truncated = error.Length <= 2000 ? error : error[..2000];

        if (message.Attempts >= _options.MaxAttempts)
        {
            _logger.MessageDeadLettered(message.Id, _options.MaxAttempts, truncated);

            await _context.Messages
                .Where(m => m.Id == message.Id)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(m => m.FailedAt, now).SetProperty(m => m.LastError, truncated),
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        var retryAt = now + RetryPolicy.DelayFor(message.Attempts, _options.BaseRetryDelay, _options.MaxRetryDelay);
        await _context.Messages
            .Where(m => m.Id == message.Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(m => m.AvailableAt, retryAt).SetProperty(m => m.LastError, truncated),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ReleaseAsync(ClaimedMessage message, string reason, CancellationToken cancellationToken)
    {
        // An unknown type is not the message's fault, so the attempt it just cost is given
        // back: it must not accumulate its way to a dead letter while a deployment finishes.
        var retryAt = _clock.UtcNow + RetryPolicy.DelayFor(1, _options.BaseRetryDelay, _options.MaxRetryDelay);
        await _context.Messages
            .Where(m => m.Id == message.Id)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(m => m.AvailableAt, retryAt)
                    .SetProperty(m => m.Attempts, message.Attempts - 1)
                    .SetProperty(m => m.LastError, reason),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
