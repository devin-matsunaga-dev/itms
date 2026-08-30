using System.Globalization;
using Itms.Contracts.Events;
using Itms.Modules.Audit.Auditing;
using Itms.Modules.Audit.Domain;

namespace Itms.UnitTests.AuditModule;

/// <summary>
/// What each domain event becomes in the trail. The mapping is written out by hand, so
/// it is asserted by hand — an event whose diff is wrong is not a failure anybody
/// notices at runtime.
/// </summary>
public sealed class EventAuditTests
{
    private static readonly Guid Ticket = Guid.CreateVersion7();
    private static readonly Guid Asset = Guid.CreateVersion7();
    private static readonly Guid Device = Guid.CreateVersion7();
    private static readonly Guid Alert = Guid.CreateVersion7();
    private static readonly Guid User = Guid.CreateVersion7();

    [Fact]
    public void Ticket_creation_records_the_fields_it_set()
    {
        var requester = Guid.CreateVersion7();
        var category = Guid.CreateVersion7();

        var described = EventAudit.Describe(
            new TicketCreated(Ticket, "INC-0001", requester, category, "High", "Printer down"));

        described.Action.ShouldBe(AuditActions.TicketCreated);
        described.EntityType.ShouldBe(AuditEntityTypes.Ticket);
        described.EntityId.ShouldBe(Ticket.ToString());
        described.Changes["ticketNumber"].ShouldBe(new(null, "INC-0001"));
        described.Changes["requesterId"].ShouldBe(new(null, requester.ToString()));
        described.Changes["categoryId"].ShouldBe(new(null, category.ToString()));
        described.Changes["priority"].ShouldBe(new(null, "High"));
        described.Changes["subject"].ShouldBe(new(null, "Printer down"));
    }

    [Fact]
    public void Assignment_records_both_sides_of_the_move()
    {
        var previous = Guid.CreateVersion7();
        var next = Guid.CreateVersion7();

        var described = EventAudit.Describe(new TicketAssigned(Ticket, "INC-0001", next, previous));

        described.Action.ShouldBe(AuditActions.TicketAssigned);
        described.Changes["assigneeId"].ShouldBe(new(previous.ToString(), next.ToString()));
    }

    [Fact]
    public void Unassignment_records_the_null_it_moved_to()
    {
        var previous = Guid.CreateVersion7();

        var described = EventAudit.Describe(new TicketAssigned(Ticket, "INC-0001", null, previous));

        described.Changes["assigneeId"].ShouldBe(new(previous.ToString(), null));
    }

    [Fact]
    public void Assignment_does_not_pretend_the_carried_ticket_number_changed() =>
        // The number rides along so other consumers can render without a lookup. §8 wants
        // changed fields only, and it did not change.
        EventAudit.Describe(new TicketAssigned(Ticket, "INC-0001", null, null))
            .Changes.Keys.ShouldBe(["assigneeId"]);

    [Fact]
    public void Status_change_records_the_transition()
    {
        var described = EventAudit.Describe(new TicketStatusChanged(Ticket, "INC-0001", "Open", "InProgress"));

        described.Action.ShouldBe(AuditActions.TicketStatusChanged);
        described.Changes["status"].ShouldBe(new("Open", "InProgress"));
    }

    [Fact]
    public void Resolution_records_when_and_what()
    {
        var resolvedAt = new DateTimeOffset(2026, 8, 31, 10, 30, 0, TimeSpan.Zero);

        var described = EventAudit.Describe(
            new TicketResolved(Ticket, "INC-0001", Guid.CreateVersion7(), resolvedAt, "Replaced the toner."));

        described.Action.ShouldBe(AuditActions.TicketResolved);
        described.Changes["resolvedAt"].After.ShouldBe(resolvedAt.ToString("O", CultureInfo.InvariantCulture));
        described.Changes["resolutionSummary"].ShouldBe(new(null, "Replaced the toner."));
    }

    [Fact]
    public void A_timestamp_is_recorded_in_utc()
    {
        var local = new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.FromHours(9));

        var described = EventAudit.Describe(new AlertResolved(Alert, Device, local, ResolvedAutomatically: true));

        // ARCHITECTURE.md invariant 11. A trail in mixed offsets cannot be ordered.
        described.Changes["resolvedAt"].After.ShouldBe("2026-08-31T03:00:00.0000000+00:00");
    }

    [Fact]
    public void Asset_assignment_records_the_holder_moving()
    {
        var previous = Guid.CreateVersion7();

        var described = EventAudit.Describe(new AssetAssigned(Asset, "AST-001", null, previous));

        described.Action.ShouldBe(AuditActions.AssetAssigned);
        described.EntityType.ShouldBe(AuditEntityTypes.Asset);
        described.EntityId.ShouldBe(Asset.ToString());
        described.Changes["assignedToUserId"].ShouldBe(new(previous.ToString(), null));
    }

    [Fact]
    public void Asset_status_change_records_the_transition()
    {
        var described = EventAudit.Describe(new AssetStatusChanged(Asset, "AST-001", "InService", "Retired"));

        described.Action.ShouldBe(AuditActions.AssetStatusChanged);
        described.Changes["status"].ShouldBe(new("InService", "Retired"));
    }

    [Fact]
    public void Going_offline_is_recorded_against_the_device_not_the_asset()
    {
        var lastSeen = new DateTimeOffset(2026, 8, 31, 8, 0, 0, TimeSpan.Zero);

        var described = EventAudit.Describe(
            new DeviceWentOffline(Device, Asset, "AST-001", Guid.CreateVersion7(), lastSeen, 3));

        described.Action.ShouldBe(AuditActions.DeviceWentOffline);
        described.EntityType.ShouldBe(AuditEntityTypes.Device);
        described.EntityId.ShouldBe(Device.ToString());
        described.Changes["state"].ShouldBe(new("online", "offline"));
        described.Changes["assetId"].ShouldBe(new(null, Asset.ToString()));
        described.Changes["consecutiveFailures"].ShouldBe(new(null, "3"));
        described.Changes["lastSeenAt"].After.ShouldBe(lastSeen.ToString("O", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Recovery_records_the_state_moving_back()
    {
        var described = EventAudit.Describe(
            new DeviceRecovered(Device, Asset, "AST-001", new DateTimeOffset(2026, 8, 31, 8, 0, 0, TimeSpan.Zero)));

        described.Action.ShouldBe(AuditActions.DeviceRecovered);
        described.Changes["state"].ShouldBe(new("offline", "online"));
    }

    [Fact]
    public void A_raised_alert_keeps_the_context_it_was_raised_with()
    {
        var described = EventAudit.Describe(
            new AlertRaised(Alert, Device, Asset, "AST-001", "HQ / Floor 2 / Room 5", "Critical", "No response"));

        described.Action.ShouldBe(AuditActions.AlertRaised);
        described.EntityType.ShouldBe(AuditEntityTypes.Alert);
        // Invariant 7: the location context is captured at the time, so it survives the
        // room being renamed afterwards.
        described.Changes["locationPath"].ShouldBe(new(null, "HQ / Floor 2 / Room 5"));
        described.Changes["severity"].ShouldBe(new(null, "Critical"));
        described.Changes["summary"].ShouldBe(new(null, "No response"));
    }

    [Fact]
    public void A_resolved_alert_records_whether_a_person_closed_it()
    {
        var described = EventAudit.Describe(
            new AlertResolved(Alert, Device, DateTimeOffset.UnixEpoch, ResolvedAutomatically: false));

        described.Changes["resolvedAutomatically"].ShouldBe(new(null, "false"));
    }

    [Fact]
    public void Deactivation_records_the_flag_and_the_name_it_had()
    {
        var described = EventAudit.Describe(new UserDeactivated(User, "Ada Lovelace"));

        described.Action.ShouldBe(AuditActions.UserDeactivated);
        described.EntityType.ShouldBe(AuditEntityTypes.User);
        described.EntityId.ShouldBe(User.ToString());
        described.Changes["isActive"].ShouldBe(new("true", "false"));
        described.Changes["displayName"].ShouldBe(new(null, "Ada Lovelace"));
    }

    [Fact]
    public void Describe_refuses_a_null_event() =>
        Should.Throw<ArgumentNullException>(() => EventAudit.Describe(null!));
}
