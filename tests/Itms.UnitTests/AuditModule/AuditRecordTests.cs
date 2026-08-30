using Itms.Modules.Audit.Domain;

namespace Itms.UnitTests.AuditModule;

/// <summary>
/// The audit row itself. It is write-once by construction, and everything about it that
/// bounds a hostile input is asserted here rather than trusted.
/// </summary>
public sealed class AuditRecordTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_fills_every_recorded_field()
    {
        var actor = Guid.CreateVersion7();
        var occurred = Now.AddMinutes(-5);

        var record = AuditRecord.Create(
            occurred,
            actor,
            "Ada Lovelace",
            "ticket.created",
            "Ticket",
            "abc",
            "203.0.113.7",
            """{"subject":{"before":null,"after":"Printer down"}}""",
            Now);

        record.Id.ShouldNotBe(Guid.Empty);
        record.OccurredAt.ShouldBe(occurred);
        record.ActorId.ShouldBe(actor);
        record.ActorName.ShouldBe("Ada Lovelace");
        record.Action.ShouldBe("ticket.created");
        record.EntityType.ShouldBe("Ticket");
        record.EntityId.ShouldBe("abc");
        record.SourceIp.ShouldBe("203.0.113.7");
        record.Changes.ShouldNotBeNull();
        record.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Create_separates_when_it_happened_from_when_it_was_written()
    {
        // A dispatcher working through a backlog must not rewrite when things happened.
        var record = AuditRecord.Create(
            Now.AddHours(-3), null, null, "device.recovered", "Device", "d", null, null, Now);

        record.OccurredAt.ShouldBe(Now.AddHours(-3));
        record.CreatedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_refuses_a_blank_action(string action) =>
        Should.Throw<ArgumentException>(() =>
            AuditRecord.Create(Now, null, null, action, "Ticket", "id", null, null, Now));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_refuses_a_blank_entity_type(string entityType) =>
        Should.Throw<ArgumentException>(() =>
            AuditRecord.Create(Now, null, null, "ticket.created", entityType, "id", null, null, Now));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_refuses_a_blank_entity_id(string entityId) =>
        Should.Throw<ArgumentException>(() =>
            AuditRecord.Create(Now, null, null, "ticket.created", "Ticket", entityId, null, null, Now));

    [Fact]
    public void Create_caps_every_text_field_at_its_column_width()
    {
        // The entity id is attacker-chosen on a failed sign-in against an account that
        // does not exist, and the actor name is whatever a display name was set to.
        var record = AuditRecord.Create(
            Now,
            null,
            new string('n', AuditRecord.ActorNameMaxLength + 50),
            new string('a', AuditRecord.ActionMaxLength + 50),
            new string('t', AuditRecord.EntityTypeMaxLength + 50),
            new string('i', AuditRecord.EntityIdMaxLength + 50),
            new string('p', AuditRecord.SourceIpMaxLength + 50),
            null,
            Now);

        record.ActorName!.Length.ShouldBe(AuditRecord.ActorNameMaxLength);
        record.Action.Length.ShouldBe(AuditRecord.ActionMaxLength);
        record.EntityType.Length.ShouldBe(AuditRecord.EntityTypeMaxLength);
        record.EntityId.Length.ShouldBe(AuditRecord.EntityIdMaxLength);
        record.SourceIp!.Length.ShouldBe(AuditRecord.SourceIpMaxLength);
    }

    [Fact]
    public void Entity_id_is_wide_enough_for_the_longest_identifier_a_sign_in_may_submit() =>
        // LoginValidator caps the submitted user name at 320, and a failed sign-in
        // against an account that does not exist has nothing else to record.
        AuditRecord.EntityIdMaxLength.ShouldBeGreaterThanOrEqualTo(320);

    /// <summary>
    /// Invariant 10, at the type level: an audit row has no way to be changed after it is
    /// created. A settable property or a second factory-shaped method would be the first
    /// step towards one.
    /// </summary>
    [Fact]
    public void Record_exposes_no_way_to_change_itself()
    {
        var properties = typeof(AuditRecord).GetProperties();

        properties.ShouldAllBe(p => p.SetMethod == null || !p.SetMethod.IsPublic);
        properties.ShouldNotBeEmpty();
    }
}
