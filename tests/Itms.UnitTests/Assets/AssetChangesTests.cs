using Itms.Modules.Assets.Domain;

namespace Itms.UnitTests.Assets;

/// <summary>
/// The snapshot diff. It is what decides how many history entries an operation owes and
/// which events it raises, so WP-2.2's done-criterion — a transfer produces exactly one
/// entry carrying both parties — is asserted here as well as end to end.
/// </summary>
public sealed class AssetChangesTests
{
    private static readonly Guid Alice = Guid.CreateVersion7();
    private static readonly Guid Bob = Guid.CreateVersion7();
    private static readonly AssetStatusRef InStock = new(Guid.CreateVersion7(), AssetStatusCode.InStock, "In Stock");
    private static readonly AssetStatusRef Deployed = new(Guid.CreateVersion7(), AssetStatusCode.Deployed, "Deployed");

    [Fact]
    public void Nothing_moving_owes_nothing()
    {
        var snapshot = Held(Alice, "Alice Adeyemi", Deployed);

        AssetChanges.Between(snapshot, snapshot).ShouldBeEmpty();
    }

    /// <summary>WP-2.2's done-criterion, at the level that decides it.</summary>
    [Fact]
    public void A_transfer_owes_exactly_one_entry_carrying_both_parties()
    {
        var changes = AssetChanges.Between(
            Held(Alice, "Alice Adeyemi", Deployed),
            Held(Bob, "Bob Okafor", Deployed));

        var change = changes.ShouldHaveSingleItem();
        change.Kind.ShouldBe(AssetChangeKind.Assignment);
        change.From.ShouldBe("Alice Adeyemi");
        change.To.ShouldBe("Bob Okafor");
    }

    [Fact]
    public void A_first_issue_out_of_stock_owes_the_assignment_then_the_status()
    {
        var changes = AssetChanges.Between(
            Held(null, null, InStock),
            Held(Alice, "Alice Adeyemi", Deployed));

        changes.Count.ShouldBe(2);

        changes[0].Kind.ShouldBe(AssetChangeKind.Assignment);
        changes[0].From.ShouldBeNull();
        changes[0].To.ShouldBe("Alice Adeyemi");

        changes[1].Kind.ShouldBe(AssetChangeKind.Status);
        changes[1].From.ShouldBe("In Stock");
        changes[1].To.ShouldBe("Deployed");
    }

    [Fact]
    public void A_return_owes_the_assignment_being_cleared()
    {
        var changes = AssetChanges.Between(
            Held(Alice, "Alice Adeyemi", Deployed),
            Held(null, null, InStock));

        changes[0].Kind.ShouldBe(AssetChangeKind.Assignment);
        changes[0].From.ShouldBe("Alice Adeyemi");
        changes[0].To.ShouldBeNull();
    }

    [Fact]
    public void A_lifecycle_move_with_the_same_holder_owes_only_the_status()
    {
        var changes = AssetChanges.Between(
            Held(Alice, "Alice Adeyemi", Deployed),
            Held(Alice, "Alice Adeyemi", InStock));

        var change = changes.ShouldHaveSingleItem();
        change.Kind.ShouldBe(AssetChangeKind.Status);
    }

    /// <summary>
    /// Two people can share a display name, and a transfer between them is still a
    /// transfer. Comparing by name would silently record nothing.
    /// </summary>
    [Fact]
    public void Two_holders_sharing_a_display_name_are_still_a_transfer()
    {
        var changes = AssetChanges.Between(
            Held(Alice, "Chris Taylor", Deployed),
            Held(Bob, "Chris Taylor", Deployed));

        var change = changes.ShouldHaveSingleItem();
        change.Kind.ShouldBe(AssetChangeKind.Assignment);
        change.From.ShouldBe("Chris Taylor");
        change.To.ShouldBe("Chris Taylor");
    }

    /// <summary>
    /// A rename is not a move. The status is compared by id, so an administrator editing
    /// "In Stock" to "In Store" does not make every subsequent operation claim the asset
    /// changed status.
    /// </summary>
    [Fact]
    public void Renaming_a_status_is_not_a_status_change()
    {
        var before = Held(Alice, "Alice Adeyemi", Deployed);
        var after = Held(Alice, "Alice Adeyemi", Deployed with { Name = "In Service" });

        AssetChanges.Between(before, after).ShouldBeEmpty();
    }

    private static AssetSnapshot Held(Guid? holderId, string? holderName, AssetStatusRef status) =>
        new(holderId, holderName, status.Id, status.Code, status.Name);
}
