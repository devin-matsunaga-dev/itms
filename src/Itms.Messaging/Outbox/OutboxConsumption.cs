namespace Itms.Messaging.Outbox;

/// <summary>
/// The record that one consumer has consumed one message. Written in the same
/// transaction as the consumer's own work, which is what turns at-least-once delivery
/// into exactly-once effects for any consumer whose side effects are in this database.
/// </summary>
public sealed class OutboxConsumption
{
    private OutboxConsumption(Guid messageId, string consumerName, DateTimeOffset consumedAt)
    {
        MessageId = messageId;
        ConsumerName = consumerName;
        ConsumedAt = consumedAt;
    }

    /// <summary>EF Core materialisation constructor.</summary>
    private OutboxConsumption() => ConsumerName = null!;

    /// <summary>The message that was consumed.</summary>
    public Guid MessageId { get; private set; }

    /// <summary>
    /// The consuming consumer's type name. Part of the key, so two consumers of the same
    /// event are tracked separately and one failing does not re-run the other.
    /// </summary>
    public string ConsumerName { get; private set; }

    /// <summary>When the consumer succeeded.</summary>
    public DateTimeOffset ConsumedAt { get; private set; }

    /// <summary>Records a consumption.</summary>
    /// <param name="messageId">The message.</param>
    /// <param name="consumerName">The consumer's type name.</param>
    /// <param name="consumedAt">The current instant.</param>
    /// <returns>The new record.</returns>
    public static OutboxConsumption Create(Guid messageId, string consumerName, DateTimeOffset consumedAt) =>
        new(messageId, consumerName, consumedAt);
}
