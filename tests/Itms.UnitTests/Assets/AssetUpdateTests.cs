using Itms.Modules.Assets.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.Assets;

/// <summary>
/// <c>Asset.Update</c> — the correction path WP-2.6b added.
/// </summary>
/// <remarks>
/// Three things are being held here. The tag, the status and the holder are outside what an
/// edit can reach, which is structural rather than checked — <see cref="AssetEdit"/> has no
/// field for any of them — so what is asserted is that the columns come out of an edit
/// unmoved. The normalisation has to match the create's, because both feed the same unique
/// index. And an edit that moves nothing has to leave the row alone, which is what keeps a
/// re-submitted form from invalidating every reader's <c>ETag</c>.
/// </remarks>
public sealed class AssetUpdateTests
{
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly Guid Editor = Guid.CreateVersion7();
    private static readonly Guid TypeId = Guid.CreateVersion7();
    private static readonly Guid OtherTypeId = Guid.CreateVersion7();
    private static readonly Guid StatusId = Guid.CreateVersion7();

    private static readonly DateTimeOffset Recorded = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 2, 11, 30, 0, TimeSpan.Zero));

    [Fact]
    public void An_edit_writes_the_descriptive_fields_and_stamps_the_row()
    {
        var asset = Recorded_asset();
        var departmentId = Guid.CreateVersion7();
        var locationId = Guid.CreateVersion7();

        asset.Update(
            Edit() with
            {
                AssetTypeId = OtherTypeId,
                Name = "Reception desktop",
                SerialNumber = "CND1234XYZ",
                Barcode = "BC-8891",
                Manufacturer = "HP",
                Model = "EliteDesk 800",
                DepartmentId = departmentId,
                DepartmentName = "Finance",
                LocationId = locationId,
                LocationPath = "CUC / Head Office / Level 2 / Room 214",
                PurchaseDate = new DateOnly(2026, 3, 14),
                WarrantyExpiresAt = new DateOnly(2029, 3, 13),
                Vendor = "Insight",
                Cost = 1249.99m,
                Notes = "Second monitor issued with it.",
            },
            _clock.UtcNow,
            Editor);

        asset.AssetTypeId.ShouldBe(OtherTypeId);
        asset.Name.ShouldBe("Reception desktop");
        asset.SerialNumber.ShouldBe("CND1234XYZ");
        asset.Barcode.ShouldBe("BC-8891");
        asset.Manufacturer.ShouldBe("HP");
        asset.Model.ShouldBe("EliteDesk 800");
        asset.DepartmentId.ShouldBe(departmentId);
        asset.DepartmentName.ShouldBe("Finance");
        asset.LocationId.ShouldBe(locationId);
        asset.LocationPath.ShouldBe("CUC / Head Office / Level 2 / Room 214");
        asset.PurchaseDate.ShouldBe(new DateOnly(2026, 3, 14));
        asset.WarrantyExpiresAt.ShouldBe(new DateOnly(2029, 3, 13));
        asset.Vendor.ShouldBe("Insight");
        asset.Cost.ShouldBe(1249.99m);
        asset.Notes.ShouldBe("Second monitor issued with it.");

        asset.UpdatedAt.ShouldBe(_clock.UtcNow);
        asset.UpdatedBy.ShouldBe(Editor);

        // The creation columns are not an edit's to touch.
        asset.CreatedAt.ShouldBe(Recorded);
        asset.CreatedBy.ShouldBe(Actor);
    }

    /// <summary>
    /// Invariant 4's immutability half, from the edit's side. The tag is not a parameter,
    /// so there is nothing to ignore — this fails the day somebody adds one.
    /// </summary>
    [Fact]
    public void An_edit_cannot_reach_the_tag()
    {
        typeof(AssetEdit)
            .GetProperties()
            .Select(property => property.Name)
            .ShouldNotContain(name => name.Contains("Tag", StringComparison.Ordinal));

        var asset = Recorded_asset();
        asset.Update(Edit() with { Name = "Renamed" }, _clock.UtcNow, Editor);

        asset.AssetTag.ShouldBe("LAP-0042");
        asset.NormalizedAssetTag.ShouldBe("LAP-0042");
    }

    /// <summary>
    /// A lifecycle move writes a history entry (invariant 5) and publishes an event; an
    /// edit does neither, so it must not be able to move either column. Both are absent
    /// from <see cref="AssetEdit"/> for that reason.
    /// </summary>
    [Fact]
    public void An_edit_cannot_reach_the_status_or_the_holder()
    {
        typeof(AssetEdit)
            .GetProperties()
            .Select(property => property.Name)
            .ShouldNotContain(name =>
                name.Contains("Status", StringComparison.Ordinal)
                || name.Contains("AssignedTo", StringComparison.Ordinal));

        var asset = Recorded_asset();
        var holder = Guid.CreateVersion7();
        asset.AssignTo(holder, "Jane Doe", Status(), null, Recorded, Actor).IsSuccess.ShouldBeTrue();

        asset.Update(Edit() with { Name = "Renamed" }, _clock.UtcNow, Editor);

        asset.AssetStatusId.ShouldBe(StatusId);
        asset.AssignedToUserId.ShouldBe(holder);
        asset.AssignedToUserName.ShouldBe("Jane Doe");
    }

    /// <summary>
    /// The same normalisation the create applies, because both feed the partial unique
    /// index on the manufacturer/serial pair. A serial corrected to a different case must
    /// still collide with the same rows.
    /// </summary>
    [Fact]
    public void The_manufacturer_and_serial_are_normalised_for_the_uniqueness_pair()
    {
        var asset = Recorded_asset();

        asset.Update(
            Edit() with { Manufacturer = " hp ", SerialNumber = " cnd1234xyz " },
            _clock.UtcNow,
            Editor);

        asset.Manufacturer.ShouldBe("hp");
        asset.NormalizedManufacturer.ShouldBe("HP");
        asset.SerialNumber.ShouldBe("cnd1234xyz");
        asset.NormalizedSerialNumber.ShouldBe("CND1234XYZ");
    }

    /// <summary>
    /// Clearing a serial has to clear the normalised column too, or the asset would keep
    /// reserving a serial it no longer claims.
    /// </summary>
    [Fact]
    public void Clearing_the_serial_clears_the_normalised_column()
    {
        var asset = Recorded_asset();
        asset.Update(Edit() with { Manufacturer = "HP", SerialNumber = "CND1234XYZ" }, _clock.UtcNow, Editor);

        asset.Update(Edit() with { Manufacturer = "HP", SerialNumber = null }, _clock.UtcNow, Editor);

        asset.SerialNumber.ShouldBeNull();
        asset.NormalizedSerialNumber.ShouldBeNull();
    }

    /// <summary>A PUT clears what it omits, and blank is the same as omitted.</summary>
    [Fact]
    public void Blank_optional_text_becomes_null()
    {
        var asset = Recorded_asset();
        asset.Update(
            Edit() with { Name = "Reception desktop", Vendor = "Insight", Notes = "Something" },
            _clock.UtcNow,
            Editor);

        asset.Update(Edit() with { Name = "   ", Vendor = "", Notes = "  " }, _clock.UtcNow, Editor);

        asset.Name.ShouldBeNull();
        asset.Vendor.ShouldBeNull();
        asset.Notes.ShouldBeNull();
    }

    /// <summary>
    /// A form re-submitted unchanged must not bump the row. If it did, every other reader
    /// holding this asset's <c>ETag</c> would be refused with 412 for a change that never
    /// happened.
    /// </summary>
    [Fact]
    public void An_edit_that_moves_nothing_leaves_the_row_untouched()
    {
        var asset = Recorded_asset();
        asset.Update(Edit() with { Name = "Reception desktop" }, Recorded, Actor);

        asset.Update(Edit() with { Name = "Reception desktop" }, _clock.UtcNow, Editor);

        asset.UpdatedAt.ShouldBe(Recorded);
        asset.UpdatedBy.ShouldBe(Actor);
    }

    /// <summary>
    /// Whitespace-only differences are not changes: the normalisation runs before the
    /// comparison, so " Reception desktop " matches the stored "Reception desktop".
    /// </summary>
    [Fact]
    public void A_difference_only_in_whitespace_is_not_a_change()
    {
        var asset = Recorded_asset();
        asset.Update(Edit() with { Name = "Reception desktop" }, Recorded, Actor);

        asset.Update(Edit() with { Name = "  Reception desktop  " }, _clock.UtcNow, Editor);

        asset.UpdatedAt.ShouldBe(Recorded);
    }

    /// <summary>
    /// What comes back is the normalised state that was applied, which is the "after" half
    /// of the diff the handler records — so the audit trail carries trimmed values rather
    /// than whatever the request happened to contain.
    /// </summary>
    [Fact]
    public void The_applied_state_is_returned_normalised()
    {
        var asset = Recorded_asset();

        var applied = asset.Update(Edit() with { Name = "  Reception desktop  " }, _clock.UtcNow, Editor);

        applied.Name.ShouldBe("Reception desktop");
        applied.ShouldBe(AssetEdit.Of(asset));
    }

    /// <summary>
    /// <c>AssetEdit.Of</c> is what both the entity and the handler compare against, so it
    /// has to read every editable column off the asset. A field added to the record and not
    /// to this factory would make the "nothing moved" check answer wrongly.
    /// </summary>
    [Fact]
    public void The_before_snapshot_round_trips_through_an_edit()
    {
        var asset = Recorded_asset();
        var wanted = Edit() with
        {
            AssetTypeId = OtherTypeId,
            Name = "Reception desktop",
            SerialNumber = "CND1234XYZ",
            Barcode = "BC-8891",
            Manufacturer = "HP",
            Model = "EliteDesk 800",
            DepartmentId = Guid.CreateVersion7(),
            DepartmentName = "Finance",
            LocationId = Guid.CreateVersion7(),
            LocationPath = "CUC / Head Office / Level 2 / Room 214",
            PurchaseDate = new DateOnly(2026, 3, 14),
            WarrantyExpiresAt = new DateOnly(2029, 3, 13),
            Vendor = "Insight",
            Cost = 1249.99m,
            Notes = "Second monitor issued with it.",
        };

        asset.Update(wanted, _clock.UtcNow, Editor);

        AssetEdit.Of(asset).ShouldBe(wanted);
    }

    [Fact]
    public void Text_longer_than_the_column_is_refused() =>
        Should.Throw<ArgumentException>(() =>
            Recorded_asset().Update(
                Edit() with { Name = new string('A', Asset.NameMaxLength + 1) },
                _clock.UtcNow,
                Editor));

    private static Asset Recorded_asset() =>
        Asset.Create(
            new NewAsset(
                "LAP-0042",
                TypeId,
                StatusId,
                Name: null,
                SerialNumber: null,
                Barcode: null,
                Manufacturer: null,
                Model: null,
                DepartmentId: null,
                DepartmentName: null,
                LocationId: null,
                LocationPath: null,
                PurchaseDate: null,
                WarrantyExpiresAt: null,
                Vendor: null,
                Cost: null,
                Notes: null),
            Recorded,
            Actor);

    /// <summary>An edit that changes nothing, for a test to move one field of.</summary>
    private static AssetEdit Edit() => new(
        TypeId,
        Name: null,
        SerialNumber: null,
        Barcode: null,
        Manufacturer: null,
        Model: null,
        DepartmentId: null,
        DepartmentName: null,
        LocationId: null,
        LocationPath: null,
        PurchaseDate: null,
        WarrantyExpiresAt: null,
        Vendor: null,
        Cost: null,
        Notes: null);

    /// <summary>
    /// Deployed rather than in stock, so <c>AssignTo</c> in the holder test moves the
    /// holder and nothing else — issuing an asset <em>out</em> of stock would also move the
    /// status, which is a second fact and not the one that test is about.
    /// </summary>
    private static AssetStatusRef Status() => new(StatusId, AssetStatusCode.Deployed, "Deployed");
}
