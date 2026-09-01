using Itms.Modules.Assets.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.Assets;

/// <summary>
/// The asset entity. WP-2.1's done-criterion is that an asset tag cannot be changed at the
/// domain level, and most of this class exists to hold that line: the immutability is
/// structural — there is no method to call — so what is asserted is the shape of the type
/// and the normalisation the unique index depends on.
/// </summary>
public sealed class AssetTests
{
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly Guid TypeId = Guid.CreateVersion7();
    private static readonly Guid StatusId = Guid.CreateVersion7();

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_new_asset_is_stamped_and_carries_what_it_was_given()
    {
        var asset = Asset.Create(Booked("LAP-0042"), _clock.UtcNow, Actor);

        asset.Id.ShouldNotBe(Guid.Empty);
        asset.AssetTag.ShouldBe("LAP-0042");
        asset.AssetTypeId.ShouldBe(TypeId);
        asset.AssetStatusId.ShouldBe(StatusId);
        asset.CreatedAt.ShouldBe(_clock.UtcNow);
        asset.CreatedBy.ShouldBe(Actor);
        asset.UpdatedAt.ShouldBe(_clock.UtcNow);
        asset.UpdatedBy.ShouldBe(Actor);
    }

    /// <summary>
    /// The assertion WP-1.2 wrote for a ticket, for the same reason: WP-2.2 adds
    /// assignment and the lifecycle transitions, and this fails the day one of those
    /// columns starts arriving pre-filled from creation — which would mean an assignment
    /// that wrote no history entry, breaking invariant 5.
    /// </summary>
    [Fact]
    public void A_new_asset_is_unassigned_and_undeleted()
    {
        var asset = Asset.Create(Booked("LAP-0043"), _clock.UtcNow, Actor);

        asset.AssignedToUserId.ShouldBeNull();
        asset.AssignedToUserName.ShouldBeNull();
        asset.DeletedAt.ShouldBeNull();
    }

    /// <summary>
    /// Invariant 4's immutability half. There is no <c>Retag</c>, no setter, and nothing
    /// else that assigns the tag — so the only honest way to assert it is to prove the type
    /// exposes no way to.
    /// </summary>
    [Fact]
    public void An_asset_tag_cannot_be_changed_after_creation()
    {
        var tag = typeof(Asset).GetProperty(nameof(Asset.AssetTag))!;

        tag.CanWrite.ShouldBeTrue("EF Core materialises through a private setter");
        tag.SetMethod!.IsPublic.ShouldBeFalse();

        // Declared on the type only, and not a property accessor: object.GetHashCode is a
        // public method whose name contains "Code", and get_AssetTag is the property being
        // read rather than a way to change it. Neither is a mutator.
        typeof(Asset)
            .GetMethods()
            .Where(method =>
                method.IsPublic
                && !method.IsStatic
                && !method.IsSpecialName
                && method.DeclaringType == typeof(Asset))
            .Select(method => method.Name)
            .ShouldNotContain(name => name.Contains("Tag", StringComparison.Ordinal));
    }

    /// <summary>
    /// The normalised column is what the unique index sits on, so "lap-0042" must not be
    /// creatable alongside "LAP-0042".
    /// </summary>
    [Fact]
    public void The_tag_is_trimmed_and_normalised_but_keeps_its_case()
    {
        var asset = Asset.Create(Booked("  lap-0042 "), _clock.UtcNow, Actor);

        // The displayed value keeps the operator's case: a tag is copied off a physical
        // label, and reading it back differently invites a second look.
        asset.AssetTag.ShouldBe("lap-0042");
        asset.NormalizedAssetTag.ShouldBe("LAP-0042");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_tag_is_refused(string tag) =>
        Should.Throw<ArgumentException>(() => Asset.Create(Booked(tag), _clock.UtcNow, Actor));

    /// <summary>
    /// Whitespace is what turns one tag into two when it is scanned, pasted, or put in a
    /// URL. Everything else about an organisation's numbering scheme is left alone.
    /// </summary>
    [Fact]
    public void A_tag_containing_whitespace_is_refused() =>
        Should.Throw<ArgumentException>(() => Asset.Create(Booked("LAP 0042"), _clock.UtcNow, Actor));

    [Fact]
    public void A_tag_longer_than_the_column_is_refused() =>
        Should.Throw<ArgumentException>(
            () => Asset.Create(Booked(new string('A', AssetTagRules.MaxLength + 1)), _clock.UtcNow, Actor));

    /// <summary>
    /// Both halves of the per-manufacturer serial rule are normalised, because the partial
    /// unique index sits on the pair — "HP"/"hp" must be one manufacturer.
    /// </summary>
    [Fact]
    public void The_manufacturer_and_serial_are_normalised_for_the_uniqueness_pair()
    {
        var asset = Asset.Create(
            Booked("LAP-0044") with { Manufacturer = " hp ", SerialNumber = " cnd1234xyz " },
            _clock.UtcNow,
            Actor);

        asset.Manufacturer.ShouldBe("hp");
        asset.NormalizedManufacturer.ShouldBe("HP");
        asset.SerialNumber.ShouldBe("cnd1234xyz");
        asset.NormalizedSerialNumber.ShouldBe("CND1234XYZ");
    }

    /// <summary>
    /// "Where present" is the whole rule: an asset with no serial collides with nothing, so
    /// the normalised columns stay null and fall outside the partial index.
    /// </summary>
    [Fact]
    public void An_asset_with_no_serial_or_manufacturer_normalises_to_null()
    {
        var asset = Asset.Create(Booked("LAP-0045"), _clock.UtcNow, Actor);

        asset.SerialNumber.ShouldBeNull();
        asset.NormalizedSerialNumber.ShouldBeNull();
        asset.Manufacturer.ShouldBeNull();
        asset.NormalizedManufacturer.ShouldBeNull();
    }

    /// <summary>Blank optional text becomes null rather than an empty string.</summary>
    [Fact]
    public void Blank_optional_text_becomes_null()
    {
        var asset = Asset.Create(
            Booked("LAP-0046") with { Name = "   ", Model = "", Vendor = " ", Notes = "  " },
            _clock.UtcNow,
            Actor);

        asset.Name.ShouldBeNull();
        asset.Model.ShouldBeNull();
        asset.Vendor.ShouldBeNull();
        asset.Notes.ShouldBeNull();
    }

    /// <summary>
    /// The lifecycle and money fields are recorded exactly as given. The cost carries no
    /// currency — see <c>Asset.Cost</c> on the single-currency assumption.
    /// </summary>
    [Fact]
    public void The_lifecycle_fields_are_recorded_as_given()
    {
        var purchased = new DateOnly(2026, 3, 14);
        var warranty = new DateOnly(2029, 3, 13);

        var asset = Asset.Create(
            Booked("LAP-0047") with
            {
                PurchaseDate = purchased,
                WarrantyExpiresAt = warranty,
                Vendor = "Insight",
                Cost = 1249.99m,
            },
            _clock.UtcNow,
            Actor);

        asset.PurchaseDate.ShouldBe(purchased);
        asset.WarrantyExpiresAt.ShouldBe(warranty);
        asset.Vendor.ShouldBe("Insight");
        asset.Cost.ShouldBe(1249.99m);
    }

    /// <summary>
    /// The department name and location path are cached on the row per §3 rule 6, because
    /// they belong to Directory and no foreign key may cross that boundary.
    /// </summary>
    [Fact]
    public void The_placement_display_strings_are_cached_on_the_row()
    {
        var departmentId = Guid.CreateVersion7();
        var locationId = Guid.CreateVersion7();

        var asset = Asset.Create(
            Booked("LAP-0048") with
            {
                DepartmentId = departmentId,
                DepartmentName = "Finance",
                LocationId = locationId,
                LocationPath = "CUC / Head Office / Level 2 / Room 214",
            },
            _clock.UtcNow,
            Actor);

        asset.DepartmentId.ShouldBe(departmentId);
        asset.DepartmentName.ShouldBe("Finance");
        asset.LocationId.ShouldBe(locationId);
        asset.LocationPath.ShouldBe("CUC / Head Office / Level 2 / Room 214");
    }

    private static NewAsset Booked(string tag) => new(
        tag,
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
        Notes: null);
}
