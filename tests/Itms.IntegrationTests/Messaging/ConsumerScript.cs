using System.Collections.Concurrent;
using Itms.Contracts.Events;
using Itms.Messaging.Abstractions;
using Npgsql;
using NpgsqlTypes;

namespace Itms.IntegrationTests.Messaging;

/// <summary>
/// What the test consumers are told to do. A singleton per test provider, so a test
/// can arm a failure for a named consumer and count how often each one ran.
/// </summary>
public sealed class ConsumerScript
{
    private readonly ConcurrentDictionary<string, int> _failuresRemaining = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _invocations = new(StringComparer.Ordinal);

    /// <summary>Makes <paramref name="consumer"/> throw its next <paramref name="times"/> invocations.</summary>
    /// <param name="consumer">The consumer's simple type name.</param>
    /// <param name="times">How many invocations should throw.</param>
    public void FailNext(string consumer, int times) => _failuresRemaining[consumer] = times;

    /// <summary>How many times <paramref name="consumer"/> has been entered, successful or not.</summary>
    /// <param name="consumer">The consumer's simple type name.</param>
    /// <returns>The invocation count.</returns>
    public int Invocations(string consumer) => _invocations.GetValueOrDefault(consumer);

    /// <summary>
    /// Records the invocation and throws if the test armed a failure. Called by every
    /// test consumer before it writes its effect.
    /// </summary>
    /// <param name="consumer">The consumer's simple type name.</param>
    /// <exception cref="InvalidOperationException">A failure was armed for this consumer.</exception>
    public void Enter(string consumer)
    {
        _invocations.AddOrUpdate(consumer, 1, (_, count) => count + 1);

        if (_failuresRemaining.TryGetValue(consumer, out var remaining) && remaining > 0)
        {
            _failuresRemaining[consumer] = remaining - 1;
            throw new InvalidOperationException($"{consumer} was told to fail.");
        }
    }
}

/// <summary>
/// Writes one row per consumption into the effects table, on the ambient connection
/// and inside the ambient transaction.
/// </summary>
/// <remarks>
/// Going through <see cref="IDbSession"/> rather than opening a connection of its own is
/// the whole point: a consumer that opened its own connection would commit independently,
/// and the retry test would then be measuring nothing.
/// </remarks>
internal static class EffectWriter
{
    public static async Task WriteAsync(
        IDbSession session,
        string consumer,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)await session.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            $"INSERT INTO {OutboxFixture.EffectsSchema}.effects (consumer, event_id) VALUES (@consumer, @event_id)",
            connection,
            (NpgsqlTransaction?)session.CurrentTransaction);

        command.Parameters.Add(new NpgsqlParameter<string>("consumer", NpgsqlDbType.Text) { TypedValue = consumer });
        command.Parameters.Add(new NpgsqlParameter<Guid>("event_id", NpgsqlDbType.Uuid) { TypedValue = eventId });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

/// <summary>A consumer of <see cref="TicketCreated"/> that records an effect row.</summary>
/// <param name="session">The ambient connection and transaction.</param>
/// <param name="script">What this consumer has been told to do.</param>
public sealed class FirstTicketConsumer(IDbSession session, ConsumerScript script) : IEventConsumer<TicketCreated>
{
    /// <summary>The name the test arms failures against and the outbox keys consumptions on.</summary>
    public const string Name = nameof(FirstTicketConsumer);

    /// <inheritdoc />
    public async Task ConsumeAsync(TicketCreated domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        script.Enter(Name);
        await EffectWriter.WriteAsync(session, Name, domainEvent.EventId, cancellationToken);
    }
}

/// <summary>A second, independent consumer of the same event, for partial-failure tests.</summary>
/// <param name="session">The ambient connection and transaction.</param>
/// <param name="script">What this consumer has been told to do.</param>
public sealed class SecondTicketConsumer(IDbSession session, ConsumerScript script) : IEventConsumer<TicketCreated>
{
    /// <summary>The name the test arms failures against.</summary>
    public const string Name = nameof(SecondTicketConsumer);

    /// <inheritdoc />
    public async Task ConsumeAsync(TicketCreated domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        script.Enter(Name);
        await EffectWriter.WriteAsync(session, Name, domainEvent.EventId, cancellationToken);
    }
}
