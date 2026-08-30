using Itms.Contracts.Auditing;
using Itms.Modules.Audit.Auditing;

namespace Itms.UnitTests.AuditModule;

/// <summary>
/// The field diff as it is stored. Most of what reaches it is text somebody typed, so
/// the bounds matter as much as the round trip.
/// </summary>
public sealed class AuditChangeSerializerTests
{
    [Fact]
    public void Round_trips_a_diff()
    {
        var changes = new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
        {
            ["name"] = new("Finance", "Finance & Operations"),
            ["code"] = new(null, "FIN"),
            ["description"] = new("Old", null),
        };

        var read = AuditChangeSerializer.Deserialize(AuditChangeSerializer.Serialize(changes));

        read.Count.ShouldBe(3);
        read["name"].ShouldBe(new AuditFieldChange("Finance", "Finance & Operations"));
        read["code"].ShouldBe(new AuditFieldChange(null, "FIN"));
        read["description"].ShouldBe(new AuditFieldChange("Old", null));
    }

    [Fact]
    public void An_action_that_changed_nothing_stores_null_rather_than_an_empty_object()
    {
        // A failed sign-in changes no field. Storing "{}" would make "no diff" and "an
        // empty diff" indistinguishable in the column.
        AuditChangeSerializer.Serialize(null).ShouldBeNull();
        AuditChangeSerializer.Serialize(new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)).ShouldBeNull();
    }

    [Fact]
    public void Deserialize_reads_a_null_column_as_no_changes() =>
        AuditChangeSerializer.Deserialize(null).ShouldBeEmpty();

    [Fact]
    public void Caps_a_value_so_one_entry_cannot_swamp_the_trail()
    {
        var changes = new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
        {
            ["description"] = new(
                new string('b', AuditChangeSerializer.ValueMaxLength + 500),
                new string('a', AuditChangeSerializer.ValueMaxLength + 500)),
        };

        var read = AuditChangeSerializer.Deserialize(AuditChangeSerializer.Serialize(changes));

        read["description"].Before!.Length.ShouldBe(AuditChangeSerializer.ValueMaxLength);
        read["description"].After!.Length.ShouldBe(AuditChangeSerializer.ValueMaxLength);
    }

    [Fact]
    public void Caps_a_field_name()
    {
        var changes = new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
        {
            [new string('f', AuditChangeSerializer.FieldNameMaxLength + 40)] = new(null, "x"),
        };

        var read = AuditChangeSerializer.Deserialize(AuditChangeSerializer.Serialize(changes));

        read.Keys.Single().Length.ShouldBe(AuditChangeSerializer.FieldNameMaxLength);
    }

    [Fact]
    public void Caps_the_number_of_fields()
    {
        var changes = Enumerable
            .Range(0, AuditChangeSerializer.MaxFields + 25)
            .ToDictionary(i => $"field{i}", i => new AuditFieldChange(null, i.ToString()), StringComparer.Ordinal);

        var read = AuditChangeSerializer.Deserialize(AuditChangeSerializer.Serialize(changes));

        read.Count.ShouldBe(AuditChangeSerializer.MaxFields);
    }

    [Fact]
    public void Drops_a_blank_field_name()
    {
        var changes = new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
        {
            ["  "] = new(null, "x"),
        };

        AuditChangeSerializer.Serialize(changes).ShouldBeNull();
    }

    [Fact]
    public void Stores_a_value_verbatim_rather_than_escaping_it_for_a_display_it_cannot_see()
    {
        // The trail records what was submitted. Encoding belongs to whatever renders it;
        // doing it here would mean the stored diff differs from what actually happened.
        var changes = new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
        {
            ["userName"] = new(null, "<script>alert(1)</script>"),
        };

        var read = AuditChangeSerializer.Deserialize(AuditChangeSerializer.Serialize(changes));

        read["userName"].After.ShouldBe("<script>alert(1)</script>");
    }
}
