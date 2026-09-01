using Itms.Platform.Results;

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
/// <b>The five lifecycle operations SPEC.md §3 names are the five methods below.</b>
/// Assignment, transfer, repair, return to service and retirement each move this entity
/// and nothing else can: there is no public setter for <see cref="AssignedToUserId"/> or
/// <see cref="AssetStatusId"/>, so a handler cannot produce one of these changes without
/// going through the method that also tells <c>AssetChanges.Between</c> what to record.
/// Invariant 5's history entry is written by the caller in the same transaction; the
/// entity's job is to refuse the moves that are not allowed, which is what
/// <see cref="AssetLifecycle"/> is consulted for.
/// </para>
/// <para>
/// <b>A transfer is not a sixth method.</b> Handing equipment from one person to another
/// is <see cref="AssignTo"/> against an asset somebody already holds — the same fact, with
/// a from-value. Making it its own method would mean two routes to one state change and
/// two chances to forget the history entry.
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
    /// Who currently holds it, or <see langword="null"/>. Moved only by
    /// <see cref="AssignTo"/>, <see cref="Return"/>, and <see cref="Retire"/>.
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
    /// invariant 5's history requirement is about the transitions the methods below make.
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

            // Not the caller's to set. AssignTo is the only route in, and its caller
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

    /// <summary>
    /// Issues the asset to somebody, or hands it from whoever holds it to somebody else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Assignment and lifecycle are separate facts, and this method treats them so.</b>
    /// Who holds a machine and where it is in its life are two different questions, and the
    /// only place they touch is the one place they must: an asset that was sitting in stock
    /// is, by being issued, deployed. Every other assignment leaves the status alone — a
    /// transfer between two people does not restart the equipment's life, and issuing a
    /// machine that is away for repair records who it is booked to without pretending it
    /// came back.
    /// </para>
    /// <para>
    /// <b>That is what makes WP-2.2's done-criterion true.</b> A transfer moves one
    /// dimension, so <c>AssetChanges.Between</c> owes exactly one history entry and it
    /// carries both parties. A first issue out of stock moves two, and owes two.
    /// </para>
    /// <para>
    /// <b>Only a terminal status refuses an assignment</b>, not the lifecycle table.
    /// <see cref="AssetLifecycle"/> governs status moves; a custom status an administrator
    /// added is not in it and must not therefore make the equipment in it unissuable.
    /// Retired, lost and disposed equipment genuinely cannot be handed to anybody, and
    /// those are the three <see cref="AssetLifecycle.IsTerminal"/> knows.
    /// </para>
    /// </remarks>
    /// <param name="userId">Who is taking it on.</param>
    /// <param name="userName">Their display name, cached onto the row per §3 rule 6.</param>
    /// <param name="current">The status the asset carries right now.</param>
    /// <param name="deployed">
    /// The deployment's <c>deployed</c> status, or <see langword="null"/> if it has none
    /// active. Needed only when the asset is being issued out of stock.
    /// </param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is doing it, or <see langword="null"/> for the system.</param>
    /// <returns>Success, or the failure that refuses it.</returns>
    public Result AssignTo(
        Guid userId,
        string userName,
        AssetStatusRef current,
        AssetStatusRef? deployed,
        DateTimeOffset now,
        Guid? actor)
    {
        RequireCurrent(current);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("An asset must be assigned to somebody.", nameof(userId));
        }

        var name = ReferenceText.Name(userName, AssignedToUserNameMaxLength, nameof(userName));

        if (AssetLifecycle.IsTerminal(current.Code))
        {
            return AssetsErrors.AssetNotAssignable(current.Name);
        }

        if (userId == AssignedToUserId)
        {
            // Not a no-op: it would raise AssetAssigned and write a history line saying the
            // asset moved from somebody to the same somebody. The call WP-1.3 made for a
            // status that is already the ticket's own.
            return AssetsErrors.AlreadyAssignedToThatUser(name);
        }

        // Moved first, so a refused transition leaves the holder untouched. From anywhere
        // but stock there is nothing to move: only the first issue changes the lifecycle.
        if (string.Equals(current.Code, AssetStatusCode.InStock, StringComparison.Ordinal))
        {
            if (deployed is not { } target)
            {
                return AssetsErrors.MissingLifecycleStatus(AssetStatusCode.Deployed);
            }

            var moved = MoveTo(current, target, now, actor);

            if (moved.IsFailure)
            {
                return moved;
            }
        }

        AssignedToUserId = userId;
        AssignedToUserName = name;
        UpdatedAt = now;
        UpdatedBy = actor;

        return Result.Success();
    }

    /// <summary>Takes the asset back off whoever holds it.</summary>
    /// <remarks>
    /// The mirror of <see cref="AssignTo"/>, and the reason <c>AssetAssigned</c> carries a
    /// nullable holder: handing equipment back is the same fact as being given it, read the
    /// other way. Equipment returned from service goes back into stock; equipment returned
    /// while it is away for repair stays in repair, because where it physically is has not
    /// changed.
    /// </remarks>
    /// <param name="current">The status the asset carries right now.</param>
    /// <param name="inStock">
    /// The deployment's <c>in-stock</c> status, or <see langword="null"/> if it has none
    /// active. Needed only when the asset is coming back out of deployment.
    /// </param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is doing it, or <see langword="null"/> for the system.</param>
    /// <returns>Success, or the failure that refuses it.</returns>
    public Result Return(AssetStatusRef current, AssetStatusRef? inStock, DateTimeOffset now, Guid? actor)
    {
        RequireCurrent(current);

        if (AssignedToUserId is null)
        {
            return AssetsErrors.AssetNotAssigned();
        }

        if (string.Equals(current.Code, AssetStatusCode.Deployed, StringComparison.Ordinal))
        {
            if (inStock is not { } target)
            {
                return AssetsErrors.MissingLifecycleStatus(AssetStatusCode.InStock);
            }

            var moved = MoveTo(current, target, now, actor);

            if (moved.IsFailure)
            {
                return moved;
            }
        }

        AssignedToUserId = null;
        AssignedToUserName = null;
        UpdatedAt = now;
        UpdatedBy = actor;

        return Result.Success();
    }

    /// <summary>Sends the asset away to be fixed.</summary>
    /// <remarks>
    /// <b>The holder is kept.</b> A laptop away at the vendor is still the machine issued
    /// to Alice, and clearing the assignment would lose the one fact somebody chasing it
    /// needs. It is also what lets <see cref="ReturnToService"/> know where the asset is
    /// going back to.
    /// </remarks>
    /// <param name="current">The status the asset carries right now.</param>
    /// <param name="repair">
    /// The deployment's <c>repair</c> status, or <see langword="null"/> if it has none
    /// active.
    /// </param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is doing it, or <see langword="null"/> for the system.</param>
    /// <returns>Success, or the conflict that refuses the move.</returns>
    public Result SendForRepair(AssetStatusRef current, AssetStatusRef? repair, DateTimeOffset now, Guid? actor)
    {
        RequireCurrent(current);

        return repair is { } target
            ? MoveTo(current, target, now, actor)
            : AssetsErrors.MissingLifecycleStatus(AssetStatusCode.Repair);
    }

    /// <summary>Brings the asset back from repair.</summary>
    /// <remarks>
    /// <b>Where it lands is decided by whether anybody still holds it</b>, at the human's
    /// direction (WP-2.2): equipment that came back to the person who had it is deployed
    /// again, and equipment nobody is waiting for goes into stock. That preserves the
    /// asset's real operational state without forcing somebody to reassign a machine that
    /// never changed hands.
    /// </remarks>
    /// <param name="current">The status the asset carries right now.</param>
    /// <param name="deployed">
    /// The deployment's <c>deployed</c> status, or <see langword="null"/>. Needed when the
    /// asset still has a holder.
    /// </param>
    /// <param name="inStock">
    /// The deployment's <c>in-stock</c> status, or <see langword="null"/>. Needed when it
    /// does not.
    /// </param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is doing it, or <see langword="null"/> for the system.</param>
    /// <returns>Success, or the failure that refuses it.</returns>
    public Result ReturnToService(
        AssetStatusRef current,
        AssetStatusRef? deployed,
        AssetStatusRef? inStock,
        DateTimeOffset now,
        Guid? actor)
    {
        RequireCurrent(current);

        var wanted = AssignedToUserId is null ? inStock : deployed;

        if (wanted is not { } target)
        {
            return AssetsErrors.MissingLifecycleStatus(
                AssignedToUserId is null ? AssetStatusCode.InStock : AssetStatusCode.Deployed);
        }

        return MoveTo(current, target, now, actor);
    }

    /// <summary>Takes the asset out of service and keeps it on the books.</summary>
    /// <remarks>
    /// <b>Retiring releases the holder</b>, at the human's direction (WP-2.2): a retired
    /// asset that is still assigned to somebody claims equipment is issued which nobody can
    /// use. So a retirement of deployed equipment moves two dimensions and writes two
    /// history lines — the release and the transition — at one instant.
    /// <para>
    /// Retired is terminal (<see cref="AssetLifecycle"/>), so this is the last thing that
    /// happens to an asset through this module's lifecycle surface.
    /// </para>
    /// </remarks>
    /// <param name="current">The status the asset carries right now.</param>
    /// <param name="retired">
    /// The deployment's <c>retired</c> status, or <see langword="null"/> if it has none
    /// active.
    /// </param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is doing it, or <see langword="null"/> for the system.</param>
    /// <returns>Success, or the conflict that refuses the move.</returns>
    public Result Retire(AssetStatusRef current, AssetStatusRef? retired, DateTimeOffset now, Guid? actor)
    {
        RequireCurrent(current);

        if (retired is not { } target)
        {
            return AssetsErrors.MissingLifecycleStatus(AssetStatusCode.Retired);
        }

        var moved = MoveTo(current, target, now, actor);

        if (moved.IsFailure)
        {
            return moved;
        }

        AssignedToUserId = null;
        AssignedToUserName = null;

        return Result.Success();
    }

    /// <summary>
    /// Applies a lifecycle move, or refuses it. The only place
    /// <see cref="AssetStatusId"/> is written.
    /// </summary>
    /// <param name="current">The status the asset carries right now.</param>
    /// <param name="target">Where it is being asked to go.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is doing it.</param>
    /// <returns>Success, or the conflict naming both statuses.</returns>
    private Result MoveTo(AssetStatusRef current, AssetStatusRef target, DateTimeOffset now, Guid? actor)
    {
        if (!AssetLifecycle.CanTransition(current.Code, target.Code))
        {
            return AssetsErrors.AssetTransitionNotAllowed(current.Name, target.Name);
        }

        AssetStatusId = target.Id;
        UpdatedAt = now;
        UpdatedBy = actor;

        return Result.Success();
    }

    /// <summary>
    /// Asserts that the caller resolved this asset's own status rather than another's.
    /// </summary>
    /// <remarks>
    /// A programming error rather than a caller's mistake — the handler reads the row by
    /// <see cref="AssetStatusId"/> — and getting it wrong would describe the move between
    /// the wrong two statuses in the history and the event. CONVENTIONS.md keeps
    /// exceptions for exactly this.
    /// </remarks>
    /// <param name="current">The status the caller believes the asset carries.</param>
    /// <exception cref="ArgumentException">It is not this asset's status.</exception>
    private void RequireCurrent(AssetStatusRef current)
    {
        if (current.Id != AssetStatusId)
        {
            throw new ArgumentException(
                "The status supplied is not the one the asset currently carries.",
                nameof(current));
        }
    }
}
