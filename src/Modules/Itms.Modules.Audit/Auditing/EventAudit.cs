using System.Globalization;
using Itms.Contracts.Auditing;
using Itms.Contracts.Events;
using Itms.Modules.Audit.Domain;

namespace Itms.Modules.Audit.Auditing;

/// <summary>What one domain event becomes in the audit trail.</summary>
/// <param name="Action">The stable action identifier.</param>
/// <param name="EntityType">The kind of entity the event is about.</param>
/// <param name="EntityId">That entity's id, as text.</param>
/// <param name="Changes">The fields the event moved.</param>
public sealed record AuditDescription(
    string Action,
    string EntityType,
    string EntityId,
    IReadOnlyDictionary<string, AuditFieldChange> Changes);

/// <summary>
/// Maps each domain event to its audit row. Pure, so the whole mapping is unit-tested
/// without a database.
/// </summary>
/// <remarks>
/// <para>
/// The mapping is written out one event at a time rather than reflected over the
/// record's properties. An event names its subject differently every time —
/// <c>TicketId</c>, <c>AssetId</c>, <c>DeviceId</c>, <c>AlertId</c> — and a reflection
/// pass would have to guess which property is the entity, which field is a diff, and
/// which is carried display text. Guessing wrong in an audit trail is not a bug you
/// notice.
/// </para>
/// <para>
/// ARCHITECTURE.md §8 says the diff records changed fields only. So a creation event
/// records its fields as null-to-value, a transition event records both sides, and an
/// event that carries denormalised display text purely for other consumers — a ticket
/// number on an assignment — does not pretend that text changed.
/// </para>
/// </remarks>
public static class EventAudit
{
    /// <summary>Describes <paramref name="domainEvent"/> as an audit row.</summary>
    /// <param name="domainEvent">The event being consumed.</param>
    /// <returns>The action, entity, and diff to record.</returns>
    /// <exception cref="NotSupportedException">
    /// The event has no mapping. That is a build-time omission rather than a runtime
    /// condition — <c>DomainEventAuditConsumer</c> only receives events it declares a
    /// binding for, and a test asserts every event in <c>Itms.Contracts</c> has one —
    /// so throwing here is how the gap announces itself if both guards are ever removed.
    /// </exception>
    public static AuditDescription Describe(DomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return domainEvent switch
        {
            TicketCreated e => new AuditDescription(
                AuditActions.TicketCreated,
                AuditEntityTypes.Ticket,
                Text(e.TicketId),
                new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
                {
                    ["ticketNumber"] = Set(e.TicketNumber),
                    ["requesterId"] = Set(Text(e.RequesterId)),
                    ["categoryId"] = Set(Text(e.CategoryId)),
                    ["priority"] = Set(e.Priority),
                    ["subject"] = Set(e.Subject),
                }),

            TicketAssigned e => new AuditDescription(
                AuditActions.TicketAssigned,
                AuditEntityTypes.Ticket,
                Text(e.TicketId),
                new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
                {
                    ["assigneeId"] = Moved(Text(e.PreviousAssigneeId), Text(e.AssigneeId)),
                }),

            TicketStatusChanged e => new AuditDescription(
                AuditActions.TicketStatusChanged,
                AuditEntityTypes.Ticket,
                Text(e.TicketId),
                new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
                {
                    ["status"] = Moved(e.FromStatus, e.ToStatus),
                }),

            TicketResolved e => new AuditDescription(
                AuditActions.TicketResolved,
                AuditEntityTypes.Ticket,
                Text(e.TicketId),
                new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
                {
                    ["resolvedAt"] = Set(Text(e.ResolvedAt)),
                    ["resolutionSummary"] = Set(e.ResolutionSummary),
                }),

            AssetAssigned e => new AuditDescription(
                AuditActions.AssetAssigned,
                AuditEntityTypes.Asset,
                Text(e.AssetId),
                new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
                {
                    ["assignedToUserId"] = Moved(Text(e.PreviousUserId), Text(e.AssignedToUserId)),
                }),

            AssetStatusChanged e => new AuditDescription(
                AuditActions.AssetStatusChanged,
                AuditEntityTypes.Asset,
                Text(e.AssetId),
                new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
                {
                    ["status"] = Moved(e.FromStatus, e.ToStatus),
                }),

            // The entity is the device, not the asset it is: invariant 6 makes the asset
            // recoverable from it, and the trail should say which record changed state.
            DeviceWentOffline e => new AuditDescription(
                AuditActions.DeviceWentOffline,
                AuditEntityTypes.Device,
                Text(e.DeviceId),
                new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
                {
                    ["state"] = Moved("online", "offline"),
                    ["assetId"] = Set(Text(e.AssetId)),
                    ["lastSeenAt"] = Set(Text(e.LastSeenAt)),
                    ["consecutiveFailures"] = Set(Text(e.ConsecutiveFailures)),
                }),

            DeviceRecovered e => new AuditDescription(
                AuditActions.DeviceRecovered,
                AuditEntityTypes.Device,
                Text(e.DeviceId),
                new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
                {
                    ["state"] = Moved("offline", "online"),
                    ["assetId"] = Set(Text(e.AssetId)),
                    ["offlineSince"] = Set(Text(e.OfflineSince)),
                }),

            AlertRaised e => new AuditDescription(
                AuditActions.AlertRaised,
                AuditEntityTypes.Alert,
                Text(e.AlertId),
                new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
                {
                    ["deviceId"] = Set(Text(e.DeviceId)),
                    ["assetTag"] = Set(e.AssetTag),
                    ["locationPath"] = Set(e.LocationPath),
                    ["severity"] = Set(e.Severity),
                    ["summary"] = Set(e.Summary),
                }),

            AlertResolved e => new AuditDescription(
                AuditActions.AlertResolved,
                AuditEntityTypes.Alert,
                Text(e.AlertId),
                new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
                {
                    ["resolvedAt"] = Set(Text(e.ResolvedAt)),
                    // Whether a person closed it or the device recovered is the first
                    // question anyone asks of a resolved alert.
                    ["resolvedAutomatically"] = Set(Text(e.ResolvedAutomatically)),
                }),

            UserDeactivated e => new AuditDescription(
                AuditActions.UserDeactivated,
                AuditEntityTypes.User,
                Text(e.UserId),
                new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
                {
                    ["isActive"] = Moved("true", "false"),
                    ["displayName"] = Set(e.DisplayName),
                }),

            _ => throw new NotSupportedException(
                $"No audit mapping for {domainEvent.GetType().FullName}. Every domain event must be audited (ARCHITECTURE.md §8)."),
        };
    }

    private static AuditFieldChange Set(string? after) => new(null, after);

    private static AuditFieldChange Moved(string? before, string? after) => new(before, after);

    private static string Text(Guid value) => value.ToString();

    private static string? Text(Guid? value) => value?.ToString();

    private static string? Text(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Text(bool value) => value ? "true" : "false";
}
