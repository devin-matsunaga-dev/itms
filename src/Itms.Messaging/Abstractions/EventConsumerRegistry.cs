using System.Reflection;
using Itms.Contracts.Events;

namespace Itms.Messaging.Abstractions;

/// <summary>
/// Every <see cref="IEventConsumer{TEvent}"/> in the solution, found by scanning rather
/// than by a registration list. Adding a reaction to an event is adding a class.
/// </summary>
public sealed class EventConsumerRegistry
{
    private readonly Dictionary<Type, IReadOnlyList<EventConsumerRegistration>> _byEventType;

    /// <summary>Builds the registry over <paramref name="registrations"/>.</summary>
    /// <param name="registrations">The discovered consumer bindings.</param>
    public EventConsumerRegistry(IEnumerable<EventConsumerRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        _byEventType = registrations
            .GroupBy(r => r.EventType)
            .ToDictionary(
                g => g.Key,
                // Ordered by name so a consumer's position in a batch does not depend on
                // reflection order, which is not guaranteed to be stable across runtimes.
                g => (IReadOnlyList<EventConsumerRegistration>)[.. g.OrderBy(r => r.Name, StringComparer.Ordinal)]);
    }

    /// <summary>Every distinct consumer class the registry found, for DI registration.</summary>
    public IReadOnlyCollection<Type> ConsumerTypes =>
        [.. _byEventType.Values.SelectMany(r => r).Select(r => r.ConsumerType).Distinct()];

    /// <summary>The consumers bound to <paramref name="eventType"/>, or an empty list when none are.</summary>
    /// <param name="eventType">A concrete domain event type.</param>
    /// <returns>The consumers, in a stable order.</returns>
    public IReadOnlyList<EventConsumerRegistration> For(Type eventType) =>
        _byEventType.TryGetValue(eventType, out var consumers) ? consumers : [];

    /// <summary>Finds every consumer binding in <paramref name="assemblies"/>.</summary>
    /// <param name="assemblies">The assemblies to scan — normally the module assemblies.</param>
    /// <returns>One registration per (consumer class, event type) pair.</returns>
    public static IReadOnlyList<EventConsumerRegistration> Discover(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var registrations = new List<EventConsumerRegistration>();

        foreach (var type in assemblies.SelectMany(a => a.GetTypes()))
        {
            if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
            {
                continue;
            }

            foreach (var consumerInterface in type.GetInterfaces())
            {
                if (!consumerInterface.IsGenericType
                    || consumerInterface.GetGenericTypeDefinition() != typeof(IEventConsumer<>))
                {
                    continue;
                }

                var eventType = consumerInterface.GetGenericArguments()[0];
                if (!typeof(DomainEvent).IsAssignableFrom(eventType))
                {
                    continue;
                }

                var method = consumerInterface.GetMethod(nameof(IEventConsumer<DomainEvent>.ConsumeAsync))
                    ?? throw new InvalidOperationException($"{consumerInterface} has no ConsumeAsync method.");

                // The interface map, not the interface method: an explicit interface
                // implementation is not reachable through the interface's own MethodInfo
                // once the instance is typed as object.
                var map = type.GetInterfaceMap(consumerInterface);
                var index = Array.IndexOf(map.InterfaceMethods, method);

                registrations.Add(new EventConsumerRegistration(eventType, type, map.TargetMethods[index]));
            }
        }

        return registrations;
    }
}
