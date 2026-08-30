using System.Reflection;
using Itms.Contracts.Events;
using Itms.Contracts.Messaging;

namespace Itms.Messaging.Abstractions;

/// <summary>
/// One consumer class bound to one event type. A class implementing
/// <see cref="IEventConsumer{TEvent}"/> twice produces two of these.
/// </summary>
/// <param name="EventType">The event this registration reacts to.</param>
/// <param name="ConsumerType">The implementing class, resolved from the scope's service provider.</param>
/// <param name="Method">The closed <c>ConsumeAsync</c> to invoke.</param>
public sealed record EventConsumerRegistration(Type EventType, Type ConsumerType, MethodInfo Method)
{
    /// <summary>
    /// The name stored in <c>outbox_consumptions</c>. It is the implementation's full
    /// name, so renaming a consumer makes it re-consume history rather than silently
    /// inherit another consumer's completions — the safe direction of that mistake.
    /// </summary>
    public string Name { get; } = ConsumerType.FullName ?? ConsumerType.Name;

    /// <summary>Invokes the consumer.</summary>
    /// <param name="consumer">The resolved consumer instance.</param>
    /// <param name="domainEvent">The event to deliver.</param>
    /// <param name="cancellationToken">Cancels the consumer.</param>
    public Task InvokeAsync(object consumer, DomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consumer);

        // Reflection is fine here: it runs once per (message, consumer), which is bounded
        // by the event rate, not by request throughput.
        return (Task)Method.Invoke(consumer, [domainEvent, cancellationToken])!;
    }
}
