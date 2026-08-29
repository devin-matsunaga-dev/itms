using Itms.Contracts;
using Itms.Contracts.Events;
using Itms.Messaging;
using Itms.Messaging.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Itms.UnitTests.Messaging;

public sealed class DomainEventSerializerTests
{
    private static readonly DomainEventTypeRegistry Registry = new([typeof(Itms.Contracts.AssemblyMarker).Assembly]);

    [Fact]
    public void A_serialised_event_round_trips_with_every_field_intact()
    {
        var original = new TicketAssigned(
            TicketId: Guid.CreateVersion7(),
            TicketNumber: "INC-0042",
            AssigneeId: Guid.CreateVersion7(),
            PreviousAssigneeId: null)
        {
            ActorId = Guid.CreateVersion7(),
        };

        var serializer = new DomainEventSerializer(Registry);
        var (eventType, payload) = DomainEventSerializer.Serialize(original);

        serializer.Deserialize(eventType, payload).ShouldBe(original);
    }

    /// <summary>
    /// The stored name must not carry the assembly: it is written into a durable table,
    /// and a row already in the outbox has to stay readable if the event moves.
    /// </summary>
    [Fact]
    public void The_stored_type_name_is_namespace_qualified_and_carries_no_assembly()
    {
        var (eventType, _) = DomainEventSerializer.Serialize(
            new AlertRaised(Guid.Empty, Guid.Empty, Guid.Empty, "AT-1", "HQ", "Critical", "Device is down"));

        eventType.ShouldBe("Itms.Contracts.Events.AlertRaised");
        eventType.ShouldNotContain(",");
    }

    /// <summary>
    /// A rolling deployment can leave a message written by a newer build in front of an
    /// older dispatcher. That is a wait, not a poisoned queue, so it must not throw.
    /// </summary>
    [Fact]
    public void An_unknown_type_name_deserialises_to_null_rather_than_throwing()
    {
        var serializer = new DomainEventSerializer(Registry);

        serializer.Deserialize("Itms.Contracts.Events.SomethingFromTheFuture", "{}").ShouldBeNull();
    }

    [Fact]
    public void The_registry_covers_every_concrete_event_in_contracts()
    {
        var declared = typeof(Itms.Contracts.AssemblyMarker).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(DomainEvent).IsAssignableFrom(t))
            .ToArray();

        declared.ShouldNotBeEmpty();
        Registry.KnownTypes.Count.ShouldBe(declared.Length);
    }

    /// <summary>
    /// The registry the container builds — not one the test hand-rolls — must know the
    /// contracts events. Both assemblies declare an <c>AssemblyMarker</c>, so an
    /// unqualified <c>typeof(Itms.Contracts.AssemblyMarker)</c> in the registration silently scans the
    /// bus's own assembly and every message becomes an undeliverable unknown type.
    /// </summary>
    [Fact]
    public void The_registered_registry_knows_the_events_declared_in_contracts()
    {
        var services = new ServiceCollection();
        services.AddMessagingCore(typeof(DomainEventSerializerTests).Assembly);

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<DomainEventTypeRegistry>();

        registry.KnownTypes.ShouldContain(typeof(TicketCreated));
        registry.TryResolve("Itms.Contracts.Events.TicketCreated", out _).ShouldBeTrue();
    }

    [Fact]
    public void The_registry_resolves_a_name_it_produced()
    {
        Registry.TryResolve(DomainEventTypeRegistry.NameOf(typeof(TicketCreated)), out var resolved).ShouldBeTrue();

        resolved.ShouldBe(typeof(TicketCreated));
    }
}
