using Itms.Contracts.Events;
using Itms.Messaging.Abstractions;

namespace Itms.UnitTests.Messaging;

public sealed class EventConsumerRegistryTests
{
    private static EventConsumerRegistry Build() =>
        new(EventConsumerRegistry.Discover([typeof(EventConsumerRegistryTests).Assembly]));

    [Fact]
    public void Discovery_finds_a_consumer_by_the_interface_it_implements()
    {
        Build().For(typeof(TicketCreated))
            .Select(r => r.ConsumerType)
            .ShouldContain(typeof(RecordingConsumer));
    }

    /// <summary>
    /// A class may react to more than one event. Each binding is separate, so failing on
    /// one event does not mark the consumer done for the other.
    /// </summary>
    [Fact]
    public void A_consumer_implementing_two_events_is_registered_against_both()
    {
        var registry = Build();

        registry.For(typeof(TicketCreated)).ShouldContain(r => r.ConsumerType == typeof(TwoEventConsumer));
        registry.For(typeof(TicketResolved)).ShouldContain(r => r.ConsumerType == typeof(TwoEventConsumer));
        registry.ConsumerTypes.Count(t => t == typeof(TwoEventConsumer)).ShouldBe(1);
    }

    /// <summary>
    /// The consumption row is keyed on this name, so it must be the implementation's, not
    /// the interface's — two consumers of one event would otherwise share a key and the
    /// second would never run.
    /// </summary>
    [Fact]
    public void The_registration_name_is_the_implementation_type()
    {
        Build().For(typeof(TicketCreated))
            .Select(r => r.Name)
            .ShouldContain(typeof(RecordingConsumer).FullName!);
    }

    [Fact]
    public void An_event_nothing_consumes_returns_an_empty_list_rather_than_null()
    {
        Build().For(typeof(UserDeactivated)).ShouldBeEmpty();
    }

    /// <summary>
    /// Reflection order is not guaranteed stable, and an unstable order would make a
    /// partial-failure test pass or fail depending on the runtime.
    /// </summary>
    [Fact]
    public void Registrations_for_one_event_are_ordered_by_name()
    {
        var names = Build().For(typeof(TicketCreated)).Select(r => r.Name).ToArray();

        names.ShouldBe([.. names.OrderBy(n => n, StringComparer.Ordinal)]);
    }

    [Fact]
    public async Task Invoking_a_registration_calls_the_consumer()
    {
        var registration = Build().For(typeof(TicketCreated)).Single(r => r.ConsumerType == typeof(RecordingConsumer));
        var consumer = new RecordingConsumer();
        var domainEvent = new TicketCreated(Guid.Empty, "INC-0001", Guid.Empty, Guid.Empty, "High", "Printer down");

        await registration.InvokeAsync(consumer, domainEvent, TestContext.Current.CancellationToken);

        consumer.Seen.ShouldBe([domainEvent]);
    }

    private sealed class RecordingConsumer : IEventConsumer<TicketCreated>
    {
        public List<TicketCreated> Seen { get; } = [];

        public Task ConsumeAsync(TicketCreated domainEvent, CancellationToken cancellationToken)
        {
            Seen.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class TwoEventConsumer : IEventConsumer<TicketCreated>, IEventConsumer<TicketResolved>
    {
        public Task ConsumeAsync(TicketCreated domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ConsumeAsync(TicketResolved domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
