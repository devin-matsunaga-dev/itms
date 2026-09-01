using Itms.Modules.Assets.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.Assets;

/// <summary>
/// The five lifecycle operations SPEC.md §3 names, at the level that decides them. Whether
/// each writes the history entry it owes is asserted end to end; what is asserted here is
/// that the entity refuses what it should and moves exactly what it should when it does
/// not.
/// </summary>
public sealed class AssetTransitionTests
{
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly Guid TypeId = Guid.CreateVersion7();
    private static readonly Guid Alice = Guid.CreateVersion7();
    private static readonly Guid Bob = Guid.CreateVersion7();

    private static readonly AssetStatusRef InStock = new(Guid.CreateVersion7(), AssetStatusCode.InStock, "In Stock");
    private static readonly AssetStatusRef Deployed = new(Guid.CreateVersion7(), AssetStatusCode.Deployed, "Deployed");
    private static readonly AssetStatusRef Repair = new(Guid.CreateVersion7(), AssetStatusCode.Repair, "Repair");
    private static readonly AssetStatusRef Retired = new(Guid.CreateVersion7(), AssetStatusCode.Retired, "Retired");
    private static readonly AssetStatusRef OnLoan = new(Guid.CreateVersion7(), "on-loan", "On Loan");

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Issuing_an_in_stock_asset_deploys_it_and_records_the_holder()
    {
        var asset = Booked(InStock);

        asset.AssignTo(Alice, "Alice Adeyemi", InStock, Deployed, _clock.UtcNow, Actor).IsSuccess.ShouldBeTrue();

        asset.AssignedToUserId.ShouldBe(Alice);
        asset.AssignedToUserName.ShouldBe("Alice Adeyemi");
        asset.AssetStatusId.ShouldBe(Deployed.Id);
        asset.UpdatedAt.ShouldBe(_clock.UtcNow);
        asset.UpdatedBy.ShouldBe(Actor);
    }

    /// <summary>
    /// A transfer moves the holder and nothing else — the half of WP-2.2's done-criterion
    /// the entity is responsible for.
    /// </summary>
    [Fact]
    public void Transferring_between_two_people_leaves_the_status_alone()
    {
        var asset = Deployed_and_held_by(Alice);

        asset.AssignTo(Bob, "Bob Okafor", Deployed, Deployed, _clock.UtcNow, Actor).IsSuccess.ShouldBeTrue();

        asset.AssignedToUserId.ShouldBe(Bob);
        asset.AssetStatusId.ShouldBe(Deployed.Id);
    }

    [Fact]
    public void Issuing_to_the_person_who_already_holds_it_is_refused()
    {
        var asset = Deployed_and_held_by(Alice);

        var result = asset.AssignTo(Alice, "Alice Adeyemi", Deployed, Deployed, _clock.UtcNow, Actor);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("assets.already_assigned_to_that_user");
    }

    [Fact]
    public void A_retired_asset_cannot_be_issued_to_anybody()
    {
        var asset = Booked(Retired);

        var result = asset.AssignTo(Alice, "Alice Adeyemi", Retired, Deployed, _clock.UtcNow, Actor);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("assets.asset_not_assignable");
        asset.AssignedToUserId.ShouldBeNull();
    }

    /// <summary>
    /// A status an administrator invented is not in the lifecycle table, and equipment in
    /// it must still be issuable — otherwise adding a status quietly makes the kit in it
    /// unusable. The status does not move, because there is no legal edge to move along.
    /// </summary>
    [Fact]
    public void An_asset_in_a_custom_status_can_still_be_issued()
    {
        var asset = Booked(OnLoan);

        asset.AssignTo(Alice, "Alice Adeyemi", OnLoan, Deployed, _clock.UtcNow, Actor).IsSuccess.ShouldBeTrue();

        asset.AssignedToUserId.ShouldBe(Alice);
        asset.AssetStatusId.ShouldBe(OnLoan.Id);
    }

    /// <summary>
    /// The deployment retired its own Deployed status, or never seeded one. Issuing out of
    /// stock has nowhere to move the asset to, and guessing would be inventing policy.
    /// </summary>
    [Fact]
    public void Issuing_out_of_stock_without_a_deployed_status_is_refused()
    {
        var asset = Booked(InStock);

        var result = asset.AssignTo(Alice, "Alice Adeyemi", InStock, deployed: null, _clock.UtcNow, Actor);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("assets.missing_lifecycle_status");
        asset.AssignedToUserId.ShouldBeNull();
    }

    [Fact]
    public void Returning_a_deployed_asset_puts_it_back_in_stock()
    {
        var asset = Deployed_and_held_by(Alice);

        asset.Return(Deployed, InStock, _clock.UtcNow, Actor).IsSuccess.ShouldBeTrue();

        asset.AssignedToUserId.ShouldBeNull();
        asset.AssignedToUserName.ShouldBeNull();
        asset.AssetStatusId.ShouldBe(InStock.Id);
    }

    [Fact]
    public void Returning_an_asset_nobody_holds_is_refused()
    {
        var asset = Booked(InStock);

        var result = asset.Return(InStock, InStock, _clock.UtcNow, Actor);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("assets.asset_not_assigned");
    }

    /// <summary>
    /// Equipment away for repair that is handed back does not come home by being handed
    /// back: where it physically is has not changed.
    /// </summary>
    [Fact]
    public void Returning_an_asset_that_is_away_for_repair_leaves_it_in_repair()
    {
        var asset = Deployed_and_held_by(Alice);
        asset.SendForRepair(Deployed, Repair, _clock.UtcNow, Actor).IsSuccess.ShouldBeTrue();

        asset.Return(Repair, InStock, _clock.UtcNow, Actor).IsSuccess.ShouldBeTrue();

        asset.AssignedToUserId.ShouldBeNull();
        asset.AssetStatusId.ShouldBe(Repair.Id);
    }

    /// <summary>The holder is kept, which is what tells return-to-service where to put it back.</summary>
    [Fact]
    public void Sending_a_deployed_asset_for_repair_keeps_its_holder()
    {
        var asset = Deployed_and_held_by(Alice);

        asset.SendForRepair(Deployed, Repair, _clock.UtcNow, Actor).IsSuccess.ShouldBeTrue();

        asset.AssetStatusId.ShouldBe(Repair.Id);
        asset.AssignedToUserId.ShouldBe(Alice);
    }

    [Fact]
    public void Sending_a_retired_asset_for_repair_is_refused()
    {
        var asset = Booked(Retired);

        var result = asset.SendForRepair(Retired, Repair, _clock.UtcNow, Actor);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("assets.transition_not_allowed");
        asset.AssetStatusId.ShouldBe(Retired.Id);
    }

    [Fact]
    public void An_asset_returning_from_repair_with_a_holder_is_deployed_again()
    {
        var asset = Deployed_and_held_by(Alice);
        asset.SendForRepair(Deployed, Repair, _clock.UtcNow, Actor);

        asset.ReturnToService(Repair, Deployed, InStock, _clock.UtcNow, Actor).IsSuccess.ShouldBeTrue();

        asset.AssetStatusId.ShouldBe(Deployed.Id);
        asset.AssignedToUserId.ShouldBe(Alice);
    }

    [Fact]
    public void An_asset_returning_from_repair_with_no_holder_goes_into_stock()
    {
        var asset = Booked(InStock);
        asset.SendForRepair(InStock, Repair, _clock.UtcNow, Actor);

        asset.ReturnToService(Repair, Deployed, InStock, _clock.UtcNow, Actor).IsSuccess.ShouldBeTrue();

        asset.AssetStatusId.ShouldBe(InStock.Id);
    }

    /// <summary>
    /// Return to service is a move out of repair, not a general "put it back": an asset
    /// that is already deployed has nowhere to return from.
    /// </summary>
    [Fact]
    public void Returning_a_deployed_asset_to_service_is_refused()
    {
        var asset = Deployed_and_held_by(Alice);

        var result = asset.ReturnToService(Deployed, Deployed, InStock, _clock.UtcNow, Actor);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("assets.transition_not_allowed");
    }

    [Fact]
    public void Retiring_a_deployed_asset_releases_its_holder()
    {
        var asset = Deployed_and_held_by(Alice);

        asset.Retire(Deployed, Retired, _clock.UtcNow, Actor).IsSuccess.ShouldBeTrue();

        asset.AssetStatusId.ShouldBe(Retired.Id);
        asset.AssignedToUserId.ShouldBeNull();
        asset.AssignedToUserName.ShouldBeNull();
    }

    /// <summary>
    /// Retired is terminal at the human's direction (WP-2.2), so a mistaken retirement has
    /// no route back through this surface. If that ever changes it is a deliberate
    /// correction workflow, and this test is what says so.
    /// </summary>
    [Fact]
    public void A_retired_asset_accepts_no_further_lifecycle_move()
    {
        var asset = Booked(Retired);

        asset.SendForRepair(Retired, Repair, _clock.UtcNow, Actor).IsFailure.ShouldBeTrue();
        asset.ReturnToService(Retired, Deployed, InStock, _clock.UtcNow, Actor).IsFailure.ShouldBeTrue();
        asset.Retire(Retired, Retired, _clock.UtcNow, Actor).IsFailure.ShouldBeTrue();
        asset.AssetStatusId.ShouldBe(Retired.Id);
    }

    /// <summary>
    /// A refused move must leave the row exactly as it was. Retiring writes two dimensions,
    /// so the order matters: the status is moved first and the holder released only after
    /// it succeeded.
    /// </summary>
    [Fact]
    public void A_refused_retirement_leaves_the_holder_in_place()
    {
        var asset = Deployed_and_held_by(Alice);

        var result = asset.Retire(Deployed, retired: null, _clock.UtcNow, Actor);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("assets.missing_lifecycle_status");
        asset.AssignedToUserId.ShouldBe(Alice);
        asset.AssetStatusId.ShouldBe(Deployed.Id);
    }

    /// <summary>
    /// The caller resolves the status row, so handing over another asset's is a programming
    /// error — and one that would describe the move between the wrong two statuses in both
    /// the history and the event.
    /// </summary>
    [Fact]
    public void Supplying_a_status_the_asset_does_not_carry_throws()
    {
        var asset = Booked(InStock);

        Should.Throw<ArgumentException>(() => asset.SendForRepair(Deployed, Repair, _clock.UtcNow, Actor));
    }

    private Asset Booked(AssetStatusRef status) =>
        Asset.Create(
            new NewAsset(
                "LAP-0100",
                TypeId,
                status.Id,
                null, null, null, null, null,
                null, null, null, null,
                null, null, null, null, null),
            _clock.UtcNow,
            Actor);

    private Asset Deployed_and_held_by(Guid userId)
    {
        var asset = Booked(InStock);
        asset.AssignTo(userId, "Alice Adeyemi", InStock, Deployed, _clock.UtcNow, Actor);
        return asset;
    }
}
