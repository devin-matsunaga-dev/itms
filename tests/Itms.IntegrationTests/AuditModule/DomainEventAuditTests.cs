using Itms.Contracts.Events;
using Itms.IntegrationTests.Identity;
using Itms.Messaging.Abstractions;
using Itms.Messaging.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace Itms.IntegrationTests.AuditModule;

/// <summary>
/// The other half of §8: everything that does raise a domain event is audited by
/// consuming it, not by the publishing module calling the writer.
/// </summary>
/// <remarks>
/// No module publishes any of these yet — Phase 1 and Phase 3 do — so the events are
/// published straight onto the bus here. That is the whole point of the seam: the audit
/// trail must be complete before the modules that fill it exist.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class DomainEventAuditTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_published_event_becomes_an_audit_row()
    {
        var ticketId = Guid.CreateVersion7();
        var actor = Guid.CreateVersion7();
        var occurred = DateTimeOffset.UtcNow.AddHours(-2);

        await PublishAsync(new TicketCreated(
            ticketId, "INC-0042", Guid.CreateVersion7(), Guid.CreateVersion7(), "High", "Printer down")
        {
            ActorId = actor,
            OccurredAt = occurred,
        });

        var row = await SingleEntryAsync("Ticket", ticketId);

        row.Action.ShouldBe("ticket.created");
        // The actor comes off the event: the dispatcher runs with no request and no
        // principal, so the publishing handler's answer is the only truthful one.
        row.ActorId.ShouldBe(actor);
        // And the event's own instant, not the moment the dispatcher reached it.
        row.OccurredAt.ShouldBe(occurred, TimeSpan.FromMilliseconds(1));
        row.Changes["ticketNumber"].After.ShouldBe("INC-0042");
        row.Changes["subject"].After.ShouldBe("Printer down");
    }

    [Fact]
    public async Task An_event_with_no_actor_is_recorded_as_the_system()
    {
        var deviceId = Guid.CreateVersion7();

        // A poller-driven transition has no human behind it, and inventing one would be
        // worse than saying so.
        await PublishAsync(new DeviceWentOffline(
            deviceId, Guid.CreateVersion7(), "AST-001", null, DateTimeOffset.UtcNow.AddMinutes(-5), 3));

        var row = await SingleEntryAsync("Device", deviceId);

        row.ActorId.ShouldBeNull();
        row.Changes["state"].ShouldBe(new("online", "offline"));
    }

    [Fact]
    public async Task The_source_address_is_null_on_an_event_derived_row()
    {
        var alertId = Guid.CreateVersion7();

        await PublishAsync(new AlertRaised(
            alertId, Guid.CreateVersion7(), Guid.CreateVersion7(), "AST-001", "HQ / Floor 2", "Critical", "No response"));

        // The event carries no request context, and a fabricated address would be worse
        // than an absent one. Recorded here so the gap is a decision, not a surprise.
        (await SingleEntryAsync("Alert", alertId)).SourceIp.ShouldBeNull();
    }

    [Fact]
    public async Task Redelivery_does_not_double_the_audit_row()
    {
        var ticketId = Guid.CreateVersion7();

        await PublishAsync(new TicketStatusChanged(ticketId, "INC-0043", "Open", "InProgress"));

        await SingleEntryAsync("Ticket", ticketId);

        // Drive the processor again by hand. Delivery is at-least-once, so the trail's
        // correctness rests on the consumption row, not on the dispatcher being careful.
        await ProcessAsync();
        await ProcessAsync();

        (await EntriesAsync("Ticket", ticketId)).Count.ShouldBe(1);
    }

    /// <summary>
    /// The dispatcher resolves every consumer in a batch from one scope, so the audit
    /// context is shared across the messages in it. A row left in the change tracker by
    /// one message would be written again by the next one's save.
    /// </summary>
    [Fact]
    public async Task Two_events_delivered_in_one_batch_produce_one_row_each()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
            var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            await session.ExecuteInTransactionAsync(
                async token =>
                {
                    await publisher.PublishAsync(new AssetStatusChanged(first, "AST-001", "InService", "InRepair"), token);
                    await publisher.PublishAsync(new AssetStatusChanged(second, "AST-002", "InService", "Retired"), token);
                },
                Token);
        }

        await ProcessAsync();

        (await SingleEntryAsync("Asset", first)).Changes["status"].After.ShouldBe("InRepair");
        (await SingleEntryAsync("Asset", second)).Changes["status"].After.ShouldBe("Retired");
    }

    [Fact]
    public async Task An_event_published_in_a_transaction_that_rolls_back_is_never_audited()
    {
        var ticketId = Guid.CreateVersion7();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
            var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            await Should.ThrowAsync<InvalidOperationException>(() => session.ExecuteInTransactionAsync(
                async token =>
                {
                    await publisher.PublishAsync(
                        new TicketResolved(ticketId, "INC-0044", Guid.CreateVersion7(), DateTimeOffset.UtcNow, "Fixed"),
                        token);

                    throw new InvalidOperationException("Deliberate rollback.");
                },
                Token));
        }

        await ProcessAsync();

        (await EntriesAsync("Ticket", ticketId)).ShouldBeEmpty();
    }

    private async Task PublishAsync(DomainEvent domainEvent)
    {
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
            var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            await session.ExecuteInTransactionAsync(
                token => publisher.PublishAsync(domainEvent, token),
                Token);
        }

        await ProcessAsync();
    }

    private async Task ProcessAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().ProcessOnceAsync(Token);
    }

    private Task<IReadOnlyList<AuditRow>> EntriesAsync(string entityType, Guid entityId) =>
        AuditQueries.ByEntityAsync(fixture.DataSource, entityType, entityId.ToString(), Token);

    private async Task<AuditRow> SingleEntryAsync(string entityType, Guid entityId)
    {
        // The host under test runs the real dispatcher on a timer as well, so the row may
        // arrive from either pass. Waiting for it beats guessing at a delay.
        await Eventually.UntilAsync(
            async () => (await EntriesAsync(entityType, entityId)).Count > 0,
            $"an audit row for {entityType} {entityId}",
            Token);

        return (await EntriesAsync(entityType, entityId)).ShouldHaveSingleItem();
    }
}
