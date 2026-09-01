namespace Itms.Modules.Assets.Domain;

/// <summary>
/// A piece of equipment the organisation owns. ARCHITECTURE.md §1 calls the asset record
/// the backbone: tickets, alerts, monitored devices, and users all reference these rows,
/// and nothing keeps a parallel copy of "the device".
/// </summary>
/// <remarks>
/// <para>
/// <b>The tag is immutable, and this class is where that is true.</b> Invariant 4 says an
/// asset tag is unique and immutable once created, and the enforcement is structural: there
/// is no setter, no <c>Retag</c>, and no method anywhere that assigns
/// <see cref="AssetTag"/> after <see cref="Create"/> has returned. A tag is stuck on a
/// physical object and referenced from paperwork nobody in this system controls, so moving
/// one would silently disconnect the record from the thing.
/// </para>
/// <para>
/// <b>What this entity deliberately cannot do yet.</b> It cannot be assigned to a person,
/// transferred, sent for repair, returned to service, or retired. Invariant 5 requires an
/// asset-history entry for every one of those, in the same transaction, and WP-2.2 is the
/// package that adds both the history table and the intent-named methods that write it.
/// The columns those methods will fill — <see cref="AssignedToUserId"/> and its cached
/// name — exist here and arrive empty. WP-1.2 left a ticket's <c>assignee_id</c> the same
/// way for the same reason, and <c>AssetTests.A_new_asset_is_unassigned_and_undeleted</c>
/// is the assertion that fails if one ever arrives pre-filled.
/// </para>
/// </remarks>
public sealed class Asset
{
    /// <summary>The longest a display name may be.</summary>
    public const int NameMaxLength = 128;

    /// <summary>The longest a serial number may be.</summary>
    public const int SerialNumberMaxLength = 128;

    /// <summary>The longest a barcode may be.</summary>
    public const int BarcodeMaxLength = 64;

    /// <summary>The longest a manufacturer name may be.</summary>
    public const int ManufacturerMaxLength = 128;

    /// <summary>The longest a model name may be.</summary>
    public const int ModelMaxLength = 128;

    /// <summary>The longest a vendor name may be.</summary>
    public const int VendorMaxLength = 128;

    /// <summary>The longest the free-text notes may be.</summary>
    public const int NotesMaxLength = 4000;

    /// <summary>The longest a cached department name may be.</summary>
    public const int DepartmentNameMaxLength = 128;

    /// <summary>The longest a cached location path may be.</summary>
    public const int LocationPathMaxLength = 512;

    /// <summary>The longest a cached holder name may be.</summary>
    public const int AssignedToUserNameMaxLength = 256;

    private Asset()
    {
        // EF Core materialisation; both are non-null in the database.
        AssetTag = null!;
        NormalizedAssetTag = null!;
    }

    /// <summary>The asset's id.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// The identifier on the physical label. Unique and immutable (invariant 4).
    /// </summary>
    public string AssetTag { get; private set; }

    /// <summary>
    /// <see cref="AssetTag"/> upper-cased. Uniqueness is enforced on this, so
    /// <c>lap-0042</c> and <c>LAP-0042</c> cannot both exist.
    /// </summary>
    public string NormalizedAssetTag { get; private set; }

    /// <summary>A human label, or <see langword="null"/> to fall back to make and model.</summary>
    public string? Name { get; private set; }

    /// <summary>The manufacturer's serial, where it has one.</summary>
    public string? SerialNumber { get; private set; }

    /// <summary>
    /// <see cref="SerialNumber"/> upper-cased, or <see langword="null"/>. Half of the
    /// "unique per manufacturer where present" rule — see <c>AssetConfiguration</c>.
    /// </summary>
    public string? NormalizedSerialNumber { get; private set; }

    /// <summary>A second scannable identifier, where the organisation uses one.</summary>
    public string? Barcode { get; private set; }

    /// <summary>Who made it.</summary>
    public string? Manufacturer { get; private set; }

    /// <summary>
    /// <see cref="Manufacturer"/> upper-cased, or <see langword="null"/>. The other half of
    /// the serial-uniqueness rule, so "HP" and "hp" are one manufacturer.
    /// </summary>
    public string? NormalizedManufacturer { get; private set; }

    /// <summary>What they call it.</summary>
    public string? Model { get; private set; }

    /// <summary>What kind of thing it is. A row in this module's own table.</summary>
    public Guid AssetTypeId { get; private set; }

    /// <summary>Where it is in its life. A row in this module's own table.</summary>
    public Guid AssetStatusId { get; private set; }

    /// <summary>
    /// Who currently holds it, or <see langword="null"/>. <b>Always null until WP-2.2</b>
    /// — see the remarks on this class.
    /// </summary>
    public Guid? AssignedToUserId { get; private set; }

    /// <summary>Their display name at the time of assignment, cached per §3 rule 6.</summary>
    public string? AssignedToUserName { get; private set; }

    /// <summary>The department that owns it, or <see langword="null"/>.</summary>
    public Guid? DepartmentId { get; private set; }

    /// <summary>That department's name, cached per §3 rule 6.</summary>
    public string? DepartmentName { get; private set; }

    /// <summary>Where it is, or <see langword="null"/>.</summary>
    public Guid? LocationId { get; private set; }

    /// <summary>That location's full path, cached per §3 rule 6.</summary>
    public string? LocationPath { get; private set; }

    /// <summary>When it was bought.</summary>
    public DateOnly? PurchaseDate { get; private set; }

    /// <summary>When the warranty runs out. WP-2.3 filters on this.</summary>
    public DateOnly? WarrantyExpiresAt { get; private set; }

    /// <summary>Who it was bought from.</summary>
    public string? Vendor { get; private set; }

    /// <summary>
    /// What it cost.
    /// </summary>
    /// <remarks>
    /// <b>Single-currency by assumption.</b> SPEC.md §3 names a cost and no currency, so
    /// there is no currency column and every figure in this table is read as being in the
    /// deployment's own currency. That is a real assumption rather than an oversight:
    /// adding a currency column later is a migration and a display change, while adding one
    /// now would invent semantics — per-asset currency? a deployment default? conversion at
    /// report time? — that nothing in the spec decides. Multi-currency support is therefore
    /// an explicit future change, recorded in STATUS.md.
    /// </remarks>
    public decimal? Cost { get; private set; }

    /// <summary>Anything else worth recording.</summary>
    public string? Notes { get; private set; }

    /// <summary>When the row was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Who created it, or <see langword="null"/> when the system did.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>When the row was last changed (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Who last changed it, or <see langword="null"/> when the system did.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>
    /// When the asset was soft-deleted, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// ARCHITECTURE.md §4 makes asset deletes soft. Nothing sets this yet — no work package
    /// names an asset delete path — and the query filter in <c>AssetConfiguration</c> is
    /// inert because of it. Both are here now for the reason WP-1.2 put a ticket's in
    /// early: added later, the filter would silently change the meaning of every list query
    /// written in the meantime.
    /// </remarks>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Records a new asset.</summary>
    /// <remarks>
    /// The asset arrives holding nobody, whatever else it arrives with. The status is the
    /// caller's to choose — an operator booking in equipment that is already deployed or
    /// already away for repair is recording a fact, not performing a transition, and
    /// invariant 5's history requirement is about the transitions WP-2.2 adds.
    /// </remarks>
    /// <param name="asset">The facts being recorded.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is recording it, or <see langword="null"/> for the system.</param>
    /// <returns>The new asset, not yet persisted.</returns>
    public static Asset Create(NewAsset asset, DateTimeOffset now, Guid? actor)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var tag = AssetTagRules.Clean(asset.AssetTag, nameof(asset));
        var serial = ReferenceText.Optional(asset.SerialNumber, SerialNumberMaxLength, nameof(asset));
        var manufacturer = ReferenceText.Optional(asset.Manufacturer, ManufacturerMaxLength, nameof(asset));

        return new Asset
        {
            // v7 so the primary key is time-ordered and its index does not fragment.
            Id = Guid.CreateVersion7(),
            AssetTag = tag,
            NormalizedAssetTag = AssetTagRules.Normalize(tag),
            Name = ReferenceText.Optional(asset.Name, NameMaxLength, nameof(asset)),
            SerialNumber = serial,
            NormalizedSerialNumber = serial?.ToUpperInvariant(),
            Barcode = ReferenceText.Optional(asset.Barcode, BarcodeMaxLength, nameof(asset)),
            Manufacturer = manufacturer,
            NormalizedManufacturer = manufacturer?.ToUpperInvariant(),
            Model = ReferenceText.Optional(asset.Model, ModelMaxLength, nameof(asset)),
            AssetTypeId = asset.AssetTypeId,
            AssetStatusId = asset.AssetStatusId,

            // Not the caller's to set. WP-2.2's AssignTo is the only route in, and it
            // writes the history entry invariant 5 requires in the same transaction.
            AssignedToUserId = null,
            AssignedToUserName = null,

            DepartmentId = asset.DepartmentId,
            DepartmentName = ReferenceText.Optional(asset.DepartmentName, DepartmentNameMaxLength, nameof(asset)),
            LocationId = asset.LocationId,
            LocationPath = ReferenceText.Optional(asset.LocationPath, LocationPathMaxLength, nameof(asset)),
            PurchaseDate = asset.PurchaseDate,
            WarrantyExpiresAt = asset.WarrantyExpiresAt,
            Vendor = ReferenceText.Optional(asset.Vendor, VendorMaxLength, nameof(asset)),
            Cost = asset.Cost,
            Notes = ReferenceText.Optional(asset.Notes, NotesMaxLength, nameof(asset)),
            CreatedAt = now,
            CreatedBy = actor,
            UpdatedAt = now,
            UpdatedBy = actor,
            DeletedAt = null,
        };
    }
}
