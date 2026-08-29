using System.Text.Json;
using System.Text.Json.Serialization;
using Itms.Contracts.Events;

namespace Itms.Messaging.Serialization;

/// <summary>
/// Turns a domain event into the outbox payload and back.
/// </summary>
/// <remarks>
/// The options are fixed here rather than taken from the host's JSON configuration.
/// An outbox row written today may be read by a process started next month, and a
/// serialisation setting changed for the sake of an API response must not be able to
/// make undelivered events unreadable.
/// </remarks>
/// <param name="registry">Resolves stored type names to CLR types.</param>
public sealed class DomainEventSerializer(DomainEventTypeRegistry registry)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = null,
        WriteIndented = false,
    };

    private readonly DomainEventTypeRegistry _registry = registry;

    /// <summary>Serialises <paramref name="domainEvent"/> for storage.</summary>
    /// <param name="domainEvent">The event.</param>
    /// <returns>The stored type name and the JSON payload.</returns>
    public static (string EventType, string Payload) Serialize(DomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var type = domainEvent.GetType();
        return (DomainEventTypeRegistry.NameOf(type), JsonSerializer.Serialize(domainEvent, type, Options));
    }

    /// <summary>Reconstructs the event a stored row describes.</summary>
    /// <param name="eventTypeName">The row's <c>event_type</c>.</param>
    /// <param name="payload">The row's JSON payload.</param>
    /// <returns>The event, or <see langword="null"/> when the type is unknown to this build.</returns>
    /// <remarks>
    /// An unknown type is not an exception. A rolling deployment can leave a message
    /// written by a newer build in front of an older dispatcher; that message should
    /// wait, not poison the queue.
    /// </remarks>
    public DomainEvent? Deserialize(string eventTypeName, string payload)
    {
        if (!_registry.TryResolve(eventTypeName, out var type))
        {
            return null;
        }

        return (DomainEvent?)JsonSerializer.Deserialize(payload, type, Options);
    }
}
