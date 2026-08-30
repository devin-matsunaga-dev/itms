using System.Runtime.CompilerServices;
using Itms.Contracts.Events;
using Itms.Contracts.Messaging;
using Itms.Modules.Audit.Auditing;

namespace Itms.UnitTests.AuditModule;

/// <summary>
/// The guard that keeps "every domain event is audited" true after this package.
/// </summary>
/// <remarks>
/// Nothing about adding an event to <c>Itms.Contracts</c> forces anyone to open the
/// consumer, and an event nobody audits is a gap in the trail that no test would
/// otherwise notice — it looks exactly like an event nothing has published yet. So the
/// build fails instead.
/// </remarks>
public sealed class DomainEventAuditCoverageTests
{
    private static readonly Type Consumer =
        typeof(Itms.Modules.Audit.AssemblyMarker).Assembly.GetType("Itms.Modules.Audit.Auditing.DomainEventAuditConsumer", throwOnError: true)!;

    /// <summary>Every concrete domain event declared in <c>Itms.Contracts</c>.</summary>
    public static TheoryData<Type> DomainEvents() =>
        [.. typeof(DomainEvent).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(DomainEvent).IsAssignableFrom(t))];

    [Fact]
    public void There_are_domain_events_to_cover() =>
        // A mistake in the discovery above would otherwise make every theory below pass
        // by having nothing to run against.
        DomainEvents().Count.ShouldBeGreaterThan(0);

    [Theory]
    [MemberData(nameof(DomainEvents))]
    public void Every_domain_event_has_a_consumer_binding(Type eventType) =>
        Consumer.GetInterfaces().ShouldContain(typeof(IEventConsumer<>).MakeGenericType(eventType));

    [Theory]
    [MemberData(nameof(DomainEvents))]
    public void Every_domain_event_has_an_audit_mapping(Type eventType)
    {
        // Constructed without running a constructor, because the mapping reads properties
        // and this test is about coverage, not about any particular payload.
        var instance = (DomainEvent)RuntimeHelpers.GetUninitializedObject(eventType);

        var described = EventAudit.Describe(instance);

        described.Action.ShouldNotBeNullOrWhiteSpace();
        described.EntityType.ShouldNotBeNullOrWhiteSpace();
        described.EntityId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void The_consumer_binds_nothing_but_domain_events() =>
        Consumer.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventConsumer<>))
            .Select(i => i.GetGenericArguments()[0])
            .ShouldAllBe(t => typeof(DomainEvent).IsAssignableFrom(t));

    [Fact]
    public void An_unmapped_event_is_refused_rather_than_silently_dropped()
    {
        // If both guards above were ever deleted, this is what makes the gap loud.
        Should.Throw<NotSupportedException>(() => EventAudit.Describe(new UnmappedEvent()));
    }

    private sealed record UnmappedEvent : DomainEvent;
}
