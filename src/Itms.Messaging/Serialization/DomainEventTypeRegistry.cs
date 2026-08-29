using System.Reflection;
using Itms.Contracts.Events;

namespace Itms.Messaging.Serialization;

/// <summary>
/// Maps between a domain event's CLR type and the string stored in the outbox.
/// </summary>
/// <remarks>
/// The stored name is the type's namespace-qualified name without assembly or version,
/// so moving an event to a different assembly does not orphan rows already written, and
/// nothing in the table can name a type to load. Deserialisation only ever resolves
/// names this registry was built with, which is why a hostile payload cannot ask for an
/// arbitrary type.
/// </remarks>
public sealed class DomainEventTypeRegistry
{
    private readonly Dictionary<string, Type> _byName;

    /// <summary>Builds a registry over every concrete <see cref="DomainEvent"/> in <paramref name="assemblies"/>.</summary>
    /// <param name="assemblies">The assemblies to scan. Normally just <c>Itms.Contracts</c>.</param>
    /// <exception cref="InvalidOperationException">Two events in different namespaces share a name.</exception>
    public DomainEventTypeRegistry(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        _byName = new Dictionary<string, Type>(StringComparer.Ordinal);

        foreach (var type in assemblies.SelectMany(a => a.GetTypes()))
        {
            if (type.IsAbstract || !typeof(DomainEvent).IsAssignableFrom(type))
            {
                continue;
            }

            var name = NameOf(type);
            if (_byName.TryGetValue(name, out var existing) && existing != type)
            {
                throw new InvalidOperationException(
                    $"Two domain events resolve to the name '{name}': {existing.AssemblyQualifiedName} and {type.AssemblyQualifiedName}.");
            }

            _byName[name] = type;
        }
    }

    /// <summary>Every event type the registry knows, for diagnostics and tests.</summary>
    public IReadOnlyCollection<Type> KnownTypes => _byName.Values;

    /// <summary>The name stored in the outbox for <paramref name="eventType"/>.</summary>
    /// <param name="eventType">A concrete domain event type.</param>
    /// <returns>The namespace-qualified type name.</returns>
    public static string NameOf(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        return eventType.FullName
            ?? throw new InvalidOperationException($"Domain event type {eventType.Name} has no full name and cannot be stored.");
    }

    /// <summary>Resolves a stored name back to its CLR type.</summary>
    /// <param name="eventTypeName">The value of the outbox row's <c>event_type</c> column.</param>
    /// <param name="eventType">The resolved type when the registry knows the name.</param>
    /// <returns><see langword="true"/> when the name resolved.</returns>
    public bool TryResolve(string eventTypeName, out Type eventType) =>
        _byName.TryGetValue(eventTypeName, out eventType!);
}
