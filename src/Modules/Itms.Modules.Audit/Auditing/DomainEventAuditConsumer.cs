using Itms.Contracts.Events;
using Itms.Contracts.Messaging;

namespace Itms.Modules.Audit.Auditing;

/// <summary>
/// Audits every domain event the system publishes (ARCHITECTURE.md §8).
/// </summary>
/// <remarks>
/// <para>
/// One class binding every event rather than eleven classes binding one each: the
/// dispatcher records consumption per (message, consumer name), so a single consumer
/// name gives the whole trail one idempotency key and one retry story. The bindings are
/// explicit interface implementations because the eleven methods differ only in their
/// parameter, and a public overload set of that shape invites someone to call the wrong
/// one.
/// </para>
/// <para>
/// The list has to stay complete as events are added, and nothing about adding an event
/// forces anyone to come here. <c>DomainEventAuditCoverageTests</c> is what makes that
/// safe: it fails the build when an event in <c>Itms.Contracts</c> has no binding on
/// this class.
/// </para>
/// <para>
/// The actor comes from the event, not from <c>ICurrentUser</c>: the dispatcher runs on
/// a background scope with no request and no principal, so the only truthful answer to
/// "who did this" is the one the publishing handler recorded. For the same reason the
/// source address is null on these rows — the event carries no request context.
/// </para>
/// </remarks>
/// <param name="recorder">The single write path into the audit table.</param>
internal sealed class DomainEventAuditConsumer(AuditRecorder recorder) :
    IEventConsumer<TicketCreated>,
    IEventConsumer<TicketAssigned>,
    IEventConsumer<TicketStatusChanged>,
    IEventConsumer<TicketResolved>,
    IEventConsumer<AssetAssigned>,
    IEventConsumer<AssetStatusChanged>,
    IEventConsumer<DeviceWentOffline>,
    IEventConsumer<DeviceRecovered>,
    IEventConsumer<AlertRaised>,
    IEventConsumer<AlertResolved>,
    IEventConsumer<UserDeactivated>
{
    Task IEventConsumer<TicketCreated>.ConsumeAsync(TicketCreated domainEvent, CancellationToken cancellationToken) =>
        AuditAsync(domainEvent, cancellationToken);

    Task IEventConsumer<TicketAssigned>.ConsumeAsync(TicketAssigned domainEvent, CancellationToken cancellationToken) =>
        AuditAsync(domainEvent, cancellationToken);

    Task IEventConsumer<TicketStatusChanged>.ConsumeAsync(TicketStatusChanged domainEvent, CancellationToken cancellationToken) =>
        AuditAsync(domainEvent, cancellationToken);

    Task IEventConsumer<TicketResolved>.ConsumeAsync(TicketResolved domainEvent, CancellationToken cancellationToken) =>
        AuditAsync(domainEvent, cancellationToken);

    Task IEventConsumer<AssetAssigned>.ConsumeAsync(AssetAssigned domainEvent, CancellationToken cancellationToken) =>
        AuditAsync(domainEvent, cancellationToken);

    Task IEventConsumer<AssetStatusChanged>.ConsumeAsync(AssetStatusChanged domainEvent, CancellationToken cancellationToken) =>
        AuditAsync(domainEvent, cancellationToken);

    Task IEventConsumer<DeviceWentOffline>.ConsumeAsync(DeviceWentOffline domainEvent, CancellationToken cancellationToken) =>
        AuditAsync(domainEvent, cancellationToken);

    Task IEventConsumer<DeviceRecovered>.ConsumeAsync(DeviceRecovered domainEvent, CancellationToken cancellationToken) =>
        AuditAsync(domainEvent, cancellationToken);

    Task IEventConsumer<AlertRaised>.ConsumeAsync(AlertRaised domainEvent, CancellationToken cancellationToken) =>
        AuditAsync(domainEvent, cancellationToken);

    Task IEventConsumer<AlertResolved>.ConsumeAsync(AlertResolved domainEvent, CancellationToken cancellationToken) =>
        AuditAsync(domainEvent, cancellationToken);

    Task IEventConsumer<UserDeactivated>.ConsumeAsync(UserDeactivated domainEvent, CancellationToken cancellationToken) =>
        AuditAsync(domainEvent, cancellationToken);

    private Task AuditAsync(DomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var description = EventAudit.Describe(domainEvent);

        return recorder.RecordAsync(
            // The event's own instant, not the moment the dispatcher reached it: a
            // backlog must not rewrite when things happened.
            domainEvent.OccurredAt,
            domainEvent.ActorId,
            // No cached actor name. An event carries display text about its subject, not
            // about whoever caused it, and looking the actor up here would be a
            // cross-module read on every single audited event.
            actorName: null,
            description.Action,
            description.EntityType,
            description.EntityId,
            sourceIp: null,
            description.Changes,
            cancellationToken);
    }
}
