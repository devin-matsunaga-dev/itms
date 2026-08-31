using Itms.Contracts.Events;
using Itms.Contracts.Messaging;
using Itms.Messaging;
using Itms.Messaging.Abstractions;
using Itms.Messaging.Outbox;
using Itms.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Itms.IntegrationTests.Messaging;

/// <summary>
/// The WP-0.4 guarantees, asserted against a real PostgreSQL: an event and the change
/// it announces commit together, delivery happens exactly once, a failing consumer
/// retries without duplicating a successful one, and a rollback publishes nothing.
/// </summary>
[Collection(OutboxTestGroup.Name)]
public sealed class OutboxTests(OutboxFixture fixture) : IAsyncLifetime
{
    private readonly OutboxFixture _fixture = fixture;
    private readonly FakeClock _clock = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private OutboxQueries Query => new(_fixture.DataSource);

    public async ValueTask InitializeAsync() => await _fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ---- The state change and its event are one transaction -------------------------

    [Fact]
    public async Task An_entity_and_its_event_are_written_in_one_transaction()
    {
        await using var provider = _fixture.BuildProvider(_clock);
        var ticketId = Guid.CreateVersion7();

        await using (var scope = provider.CreateAsyncScope())
        {
            var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
            var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            await session.ExecuteInTransactionAsync(
                async ct =>
                {
                    var connection = (NpgsqlConnection)await session.OpenAsync(ct);
                    await OutboxQueries.InsertTicketAsync(connection, (NpgsqlTransaction?)session.CurrentTransaction, ticketId, "Printer down", ct);
                    await publisher.PublishAsync(TicketCreated(ticketId), ct);
                },
                Ct);
        }

        (await Query.CountTicketsAsync(Ct)).ShouldBe(1);
        (await Query.CountMessagesAsync(Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task A_rolled_back_transaction_publishes_nothing()
    {
        await using var provider = _fixture.BuildProvider(_clock);
        var ticketId = Guid.CreateVersion7();

        await using (var scope = provider.CreateAsyncScope())
        {
            var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
            var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            await Should.ThrowAsync<InvalidOperationException>(() =>
                session.ExecuteInTransactionAsync(
                    async ct =>
                    {
                        var connection = (NpgsqlConnection)await session.OpenAsync(ct);
                        await OutboxQueries.InsertTicketAsync(connection, (NpgsqlTransaction?)session.CurrentTransaction, ticketId, "Printer down", ct);
                        await publisher.PublishAsync(TicketCreated(ticketId), ct);

                        throw new InvalidOperationException("The handler failed after publishing.");
                    },
                    Ct));
        }

        // Neither half survived. An outbox that kept the event here would announce a
        // ticket that does not exist — the exact failure the outbox exists to prevent.
        (await Query.CountTicketsAsync(Ct)).ShouldBe(0);
        (await Query.CountMessagesAsync(Ct)).ShouldBe(0);
    }

    /// <summary>
    /// Publishing outside a transaction would commit the event on its own, which is the
    /// same failure with a friendlier face. It is refused rather than tolerated.
    /// </summary>
    [Fact]
    public async Task Publishing_outside_a_transaction_is_refused()
    {
        await using var provider = _fixture.BuildProvider(_clock);
        await using var scope = provider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        await Should.ThrowAsync<InvalidOperationException>(() => publisher.PublishAsync(TicketCreated(Guid.CreateVersion7()), Ct));

        (await Query.CountMessagesAsync(Ct)).ShouldBe(0);
    }

    // ---- Delivery happens exactly once ----------------------------------------------

    [Fact]
    public async Task The_dispatcher_delivers_an_event_to_every_consumer_exactly_once()
    {
        await using var provider = _fixture.BuildProvider(_clock);
        await PublishAsync(provider, TicketCreated(Guid.CreateVersion7()));

        (await ProcessOnceAsync(provider)).ShouldBe(1);

        // Further passes must find nothing: the message is processed and, even if it were
        // re-claimed, the consumption rows would stop the consumers running again.
        _clock.Advance(TimeSpan.FromHours(1));
        (await ProcessOnceAsync(provider)).ShouldBe(0);

        (await Query.CountEffectsAsync(FirstTicketConsumer.Name, Ct)).ShouldBe(1);
        (await Query.CountEffectsAsync(SecondTicketConsumer.Name, Ct)).ShouldBe(1);
        (await Query.CountConsumptionsAsync(Ct)).ShouldBe(2);
        (await Query.CountProcessedAsync(Ct)).ShouldBe(1);
    }

    /// <summary>
    /// The dispatcher must not depend on a consumer existing. Phase 0 publishes events
    /// whose Phase 1 consumers have not been written yet, and those must not accumulate.
    /// </summary>
    [Fact]
    public async Task An_event_no_consumer_reacts_to_is_completed_rather_than_left_outstanding()
    {
        await using var provider = _fixture.BuildProvider(_clock);
        await PublishAsync(provider, new UserDeactivated(Guid.CreateVersion7(), "Someone"));

        await ProcessOnceAsync(provider);

        (await Query.CountProcessedAsync(Ct)).ShouldBe(1);
        (await Query.CountConsumptionsAsync(Ct)).ShouldBe(0);
    }

    // ---- A failing consumer retries without duplicating a successful one -------------

    [Fact]
    public async Task A_failing_consumer_is_retried_and_the_successful_one_is_not_re_run()
    {
        await using var provider = _fixture.BuildProvider(_clock);
        var script = provider.GetRequiredService<ConsumerScript>();
        script.FailNext(SecondTicketConsumer.Name, times: 1);

        var published = TicketCreated(Guid.CreateVersion7());
        await PublishAsync(provider, published);

        await ProcessOnceAsync(provider);

        // First pass: one consumer committed its effect and its consumption together; the
        // other rolled back and left neither.
        (await Query.CountEffectsAsync(FirstTicketConsumer.Name, Ct)).ShouldBe(1);
        (await Query.CountEffectsAsync(SecondTicketConsumer.Name, Ct)).ShouldBe(0);
        (await Query.CountConsumptionsAsync(Ct)).ShouldBe(1);
        (await Query.CountProcessedAsync(Ct)).ShouldBe(0);

        var state = await Query.MessageStateAsync(published.EventId, Ct);
        state.Attempts.ShouldBe(1);
        state.LastError.ShouldNotBeNull().ShouldContain(SecondTicketConsumer.Name);

        // The retry is not due yet, so a pass now finds nothing to claim.
        (await ProcessOnceAsync(provider)).ShouldBe(0);

        _clock.Advance(TimeSpan.FromMinutes(1));
        (await ProcessOnceAsync(provider)).ShouldBe(1);

        // Second pass: the failed consumer ran and succeeded, and the successful one was
        // skipped rather than duplicating its side effect.
        (await Query.CountEffectsAsync(FirstTicketConsumer.Name, Ct)).ShouldBe(1);
        (await Query.CountEffectsAsync(SecondTicketConsumer.Name, Ct)).ShouldBe(1);
        (await Query.CountConsumptionsAsync(Ct)).ShouldBe(2);
        (await Query.CountProcessedAsync(Ct)).ShouldBe(1);

        script.Invocations(FirstTicketConsumer.Name).ShouldBe(1);
        script.Invocations(SecondTicketConsumer.Name).ShouldBe(2);
    }

    /// <summary>
    /// A claimed message is invisible for the lease duration. Without that, a second
    /// dispatcher — or a fast second pass — would work a message already in flight.
    /// </summary>
    [Fact]
    public async Task A_claimed_message_is_not_reclaimed_before_its_retry_is_due()
    {
        await using var provider = _fixture.BuildProvider(_clock, o => o.BaseRetryDelay = TimeSpan.FromMinutes(5));
        provider.GetRequiredService<ConsumerScript>().FailNext(FirstTicketConsumer.Name, times: 10);

        var published = TicketCreated(Guid.CreateVersion7());
        await PublishAsync(provider, published);

        await ProcessOnceAsync(provider);

        _clock.Advance(TimeSpan.FromMinutes(4));
        (await ProcessOnceAsync(provider)).ShouldBe(0);

        _clock.Advance(TimeSpan.FromMinutes(2));
        (await ProcessOnceAsync(provider)).ShouldBe(1);
    }

    [Fact]
    public async Task A_consumer_that_never_succeeds_is_parked_after_the_attempt_cap()
    {
        await using var provider = _fixture.BuildProvider(
            _clock,
            o =>
            {
                o.MaxAttempts = 3;
                o.BaseRetryDelay = TimeSpan.FromSeconds(1);
                o.MaxRetryDelay = TimeSpan.FromSeconds(1);
            });

        provider.GetRequiredService<ConsumerScript>().FailNext(FirstTicketConsumer.Name, times: 100);
        await PublishAsync(provider, TicketCreated(Guid.CreateVersion7()));

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await ProcessOnceAsync(provider);
            _clock.Advance(TimeSpan.FromSeconds(2));
        }

        (await Query.CountFailedAsync(Ct)).ShouldBe(1);

        // Parked, not deleted: a dead-lettered event is the evidence that something needs
        // looking at, and it must never be claimed again on its own.
        (await Query.CountMessagesAsync(Ct)).ShouldBe(1);
        (await ProcessOnceAsync(provider)).ShouldBe(0);
    }

    // ---- Helpers --------------------------------------------------------------------

    private static TicketCreated TicketCreated(Guid ticketId) =>
        new(ticketId, "INC-0001", Guid.CreateVersion7(), Guid.CreateVersion7(), "High", "Printer down");

    private static async Task PublishAsync(IServiceProvider provider, DomainEvent domainEvent)
    {
        await using var scope = provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        await session.ExecuteInTransactionAsync(ct => publisher.PublishAsync(domainEvent, ct), Ct);
    }

    private static async Task<int> ProcessOnceAsync(IServiceProvider provider)
    {
        // A fresh scope per pass, exactly as OutboxDispatcher does it.
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().ProcessOnceAsync(Ct);
    }
}
