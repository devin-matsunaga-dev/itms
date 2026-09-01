using Itms.Modules.Assets.Domain;

namespace Itms.UnitTests.Assets;

/// <summary>
/// One line of an asset's timeline. It is write-once, so what is asserted here is the
/// factory's guards and the rule that an over-long value can never stop the change it
/// describes from being recorded.
/// </summary>
public sealed class AssetHistoryEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid AssetId = Guid.CreateVersion7();
    private static readonly Guid Actor = Guid.CreateVersion7();

    [Fact]
    public void An_entry_carries_what_it_was_given()
    {
        var entry = AssetHistoryEntry.Record(
            AssetId,
            new AssetChange(AssetChangeKind.Assignment, null, "Alice Adeyemi"),
            sequence: 0,
            Now,
            "issued at onboarding",
            Actor,
            "Tess Technician");

        entry.Id.ShouldNotBe(Guid.Empty);
        entry.AssetId.ShouldBe(AssetId);
        entry.Kind.ShouldBe(AssetChangeKind.Assignment);
        entry.FromValue.ShouldBeNull();
        entry.ToValue.ShouldBe("Alice Adeyemi");
        entry.Note.ShouldBe("issued at onboarding");
        entry.OccurredAt.ShouldBe(Now);
        entry.Sequence.ShouldBe(0);
        entry.ActorId.ShouldBe(Actor);
        entry.ActorName.ShouldBe("Tess Technician");
    }

    /// <summary>
    /// The audit row's rule, restated: a value somebody made too long must not be able to
    /// lose the whole entry. It is capped, and the change is still recorded.
    /// </summary>
    [Fact]
    public void An_over_long_value_note_or_actor_name_is_capped_rather_than_refused()
    {
        var entry = AssetHistoryEntry.Record(
            AssetId,
            new AssetChange(
                AssetChangeKind.Assignment,
                new string('a', AssetHistoryEntry.ValueMaxLength + 50),
                new string('b', AssetHistoryEntry.ValueMaxLength + 50)),
            sequence: 0,
            Now,
            new string('n', AssetHistoryEntry.NoteMaxLength + 50),
            Actor,
            new string('c', AssetHistoryEntry.ActorNameMaxLength + 50));

        entry.FromValue!.Length.ShouldBe(AssetHistoryEntry.ValueMaxLength);
        entry.ToValue!.Length.ShouldBe(AssetHistoryEntry.ValueMaxLength);
        entry.Note!.Length.ShouldBe(AssetHistoryEntry.NoteMaxLength);
        entry.ActorName!.Length.ShouldBe(AssetHistoryEntry.ActorNameMaxLength);
    }

    [Fact]
    public void An_entry_must_belong_to_an_asset()
    {
        Should.Throw<ArgumentException>(() => AssetHistoryEntry.Record(
            Guid.Empty,
            new AssetChange(AssetChangeKind.Status, "In Stock", "Deployed"),
            sequence: 0,
            Now,
            note: null,
            Actor,
            "Tess Technician"));
    }

    [Fact]
    public void A_sequence_cannot_be_negative()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => AssetHistoryEntry.Record(
            AssetId,
            new AssetChange(AssetChangeKind.Status, "In Stock", "Deployed"),
            sequence: -1,
            Now,
            note: null,
            Actor,
            "Tess Technician"));
    }

    /// <summary>
    /// The system does things too — a future import or an automated retirement — and an
    /// entry with no actor has to be recordable rather than throwing.
    /// </summary>
    [Fact]
    public void An_entry_can_have_no_actor()
    {
        var entry = AssetHistoryEntry.Record(
            AssetId,
            new AssetChange(AssetChangeKind.Status, "In Stock", "Deployed"),
            sequence: 0,
            Now,
            note: null,
            actorId: null,
            actorName: null);

        entry.ActorId.ShouldBeNull();
        entry.ActorName.ShouldBeNull();
        entry.Note.ShouldBeNull();
    }
}
