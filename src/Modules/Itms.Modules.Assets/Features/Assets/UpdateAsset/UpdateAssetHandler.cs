using System.Globalization;
using Itms.Contracts.Auditing;
using Itms.Contracts.Lookups;
using Itms.Modules.Assets.Auditing;
using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Itms.Modules.Assets.Persistence.Configurations;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.Assets.UpdateAsset;

/// <summary>
/// Corrects an asset's descriptive facts.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the only asset write that is not a lifecycle operation</b>, which is why it
/// does not go through <c>AssetLifecycleMutation</c>. That envelope exists to make four
/// operations agree about taking a snapshot, writing history in the same transaction, and
/// publishing the two events §5 names — and an edit does none of those three. What it does
/// share is the precondition handling, and that is eleven lines rather than a shape bent to
/// fit.
/// </para>
/// <para>
/// <b>No history entry, and no domain event.</b> WP-2.6b names one audit action and
/// nothing else: invariant 5 lists the five moves that owe a history entry and an edit is
/// not among them, and ARCHITECTURE.md §5's event list has no <c>AssetUpdated</c>. The
/// consequence is worth knowing — a corrected serial number appears in the audit trail and
/// <em>not</em> on the asset's own timeline, which is where a technician would look for it.
/// That gap is recorded in STATUS.md rather than closed here.
/// </para>
/// <para>
/// <b>A retired asset type is refused only when the asset is moving into one.</b> The
/// create refuses one outright, and rightly — no new equipment should be classified as
/// something the organisation has stopped using. But an asset already classified as a type
/// somebody has since retired must stay editable, or retiring a type would freeze every
/// asset in it. So the check is on the change, not on the state.
/// </para>
/// </remarks>
/// <param name="database">The assets context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
/// <param name="departments">How this module reads Directory's departments.</param>
/// <param name="locations">How this module reads Directory's locations.</param>
internal sealed class UpdateAssetHandler(
    AssetsDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IDepartmentLookup departments,
    ILocationLookup locations)
{
    /// <summary>Applies <paramref name="request"/> to the asset with <paramref name="assetId"/>.</summary>
    /// <param name="assetId">The asset being corrected.</param>
    /// <param name="request">The facts as they should now read.</param>
    /// <param name="expectedVersions">
    /// The row versions the caller's <c>If-Match</c> will accept, or <see langword="null"/>
    /// when it stated no precondition.
    /// </param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>The asset as it now stands, or the failure that stopped the edit.</returns>
    public async Task<Result<AssetDetail>> HandleAsync(
        Guid assetId,
        UpdateAssetRequest request,
        IReadOnlySet<uint>? expectedVersions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Error? failure = null;
        AssetDetail? updated = null;

        // Resolved before the transaction opens, as the create does: these are reads
        // against another module and holding a write transaction across them would widen
        // it for no benefit.
        var placement = await ResolvePlacementAsync(request, cancellationToken).ConfigureAwait(false);

        if (placement.Failure is not null)
        {
            return placement.Failure;
        }

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                // Tracked, not AsNoTracking: this is a write, and the xmin token only does
                // its job on a tracked entity. The soft-delete query filter applies, so a
                // deleted asset is a 404 rather than a silent write.
                var asset = await database.Assets
                    .FirstOrDefaultAsync(candidate => candidate.Id == assetId, token)
                    .ConfigureAwait(false);

                if (asset is null)
                {
                    failure = AssetsErrors.AssetNotFound();
                    return;
                }

                var entry = database.Entry(asset);

                // The caller's precondition, checked before anything is attempted — the
                // whole point of the 412. The row is already loaded and locked by the read,
                // so this cannot itself race.
                if (expectedVersions is not null
                    && !expectedVersions.Contains(entry.Property<uint>(AssetConfiguration.VersionProperty).CurrentValue))
                {
                    failure = AssetsErrors.AssetPreconditionFailed();
                    return;
                }

                var type = await ResolveTypeAsync(asset, request.AssetTypeId, token).ConfigureAwait(false);

                if (type.Failure is not null)
                {
                    failure = type.Failure;
                    return;
                }

                var before = AssetEdit.Of(asset);

                var after = asset.Update(
                    new AssetEdit(
                        type.Type!.Id,
                        request.Name,
                        request.SerialNumber,
                        request.Barcode,
                        request.Manufacturer,
                        request.Model,
                        placement.DepartmentId,
                        placement.DepartmentName,
                        placement.LocationId,
                        placement.LocationPath,
                        request.PurchaseDate,
                        request.WarrantyExpiresAt,
                        request.Vendor,
                        request.Cost,
                        request.Notes),
                    clock.UtcNow,
                    currentUser.UserId);

                failure = await CheckSerialUniquenessAsync(asset, token).ConfigureAwait(false);

                if (failure is not null)
                {
                    return;
                }

                try
                {
                    await database.SaveChangesAsync(token).ConfigureAwait(false);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Somebody moved the asset between the read and the write. A 409 the
                    // client can retry, not the 500 an unhandled one would be.
                    failure = AssetsErrors.AssetChangedConcurrently();
                    return;
                }

                var changes = Diff(before, after);

                // An edit that moved nothing writes no audit row. Asset.Update leaves the
                // row untouched in that case, so an entry here would be claiming a change
                // the database cannot show — and ARCHITECTURE.md §8 asks for changed fields
                // only, of which there would be none.
                if (changes.Count > 0)
                {
                    // Inside the transaction, so an edit that rolls back leaves no entry
                    // claiming it happened. SPEC.md §15 makes asset modifications mandatory
                    // audit coverage; this write publishes no domain event, so IAuditWriter
                    // is the route — see AssetsAudit.AssetUpdated.
                    await audit.WriteAsync(
                        new AuditEntry(
                            AssetsAudit.AssetUpdated,
                            AssetsAudit.AssetEntityType,
                            asset.Id.ToString(),
                            changes),
                        token).ConfigureAwait(false);
                }

                var status = await database.AssetStatuses
                    .AsNoTracking()
                    .Where(candidate => candidate.Id == asset.AssetStatusId)
                    .Select(candidate => new AssetStatusRef(candidate.Id, candidate.Code, candidate.Name))
                    .FirstAsync(token)
                    .ConfigureAwait(false);

                updated = new AssetDetail(
                    AssetResponse.From(asset, type.Type.Id, type.Type.Name, status),
                    // Read back off the tracked entry after the write: xmin is
                    // ValueGeneratedOnAddOrUpdate, so EF refreshes it with the UPDATE. An
                    // edit that changed nothing issues no UPDATE and the tag is unchanged,
                    // which is the honest answer — nobody else's precondition was broken.
                    entry.Property<uint>(AssetConfiguration.VersionProperty).CurrentValue);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? failure : updated!;
    }

    /// <summary>
    /// Which fields actually moved, camel-cased as the client names them.
    /// </summary>
    /// <remarks>
    /// <c>AssetsAudit.Moved</c> records a field only when the two values differ, so this
    /// lists every editable field and the diff keeps the ones that changed. Ids are
    /// recorded rather than display names, for the reason the lifecycle events record
    /// codes: the trail is permanent and machine-readable, and a name an administrator can
    /// edit would make two entries written a year apart describe the same change
    /// differently.
    /// </remarks>
    private static Dictionary<string, AuditFieldChange> Diff(AssetEdit before, AssetEdit after) =>
        AssetsAudit.Changes()
            .Moved("assetTypeId", Text(before.AssetTypeId), Text(after.AssetTypeId))
            .Moved("name", before.Name, after.Name)
            .Moved("serialNumber", before.SerialNumber, after.SerialNumber)
            .Moved("barcode", before.Barcode, after.Barcode)
            .Moved("manufacturer", before.Manufacturer, after.Manufacturer)
            .Moved("model", before.Model, after.Model)
            .Moved("departmentId", Text(before.DepartmentId), Text(after.DepartmentId))
            .Moved("locationId", Text(before.LocationId), Text(after.LocationId))
            .Moved("purchaseDate", Text(before.PurchaseDate), Text(after.PurchaseDate))
            .Moved("warrantyExpiresAt", Text(before.WarrantyExpiresAt), Text(after.WarrantyExpiresAt))
            .Moved("vendor", before.Vendor, after.Vendor)
            .Moved("cost", Text(before.Cost), Text(after.Cost))
            .Moved("notes", before.Notes, after.Notes);

    private static string? Text(Guid? value) => value?.ToString();

    private static string? Text(DateOnly? value) => value?.ToString("O", CultureInfo.InvariantCulture);

    private static string? Text(decimal? value) => value?.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Refuses a serial the same manufacturer has already used on another asset.
    /// </summary>
    /// <remarks>
    /// The tag is not re-checked, because it cannot have moved: <c>AssetEdit</c> carries
    /// no tag and <see cref="Asset"/> exposes no way to write one.
    /// </remarks>
    private async Task<Error?> CheckSerialUniquenessAsync(Asset asset, CancellationToken cancellationToken)
    {
        // "Unique per manufacturer where present": nothing to check unless both halves are
        // there, which is what the partial index says too.
        if (asset.NormalizedManufacturer is null || asset.NormalizedSerialNumber is null)
        {
            return null;
        }

        // IgnoreQueryFilters, as on the create: a soft-deleted asset keeps its serial
        // reserved, so the check has to see rows the ordinary filter hides. The asset being
        // edited is excluded, or every edit that left the serial alone would collide with
        // itself.
        var taken = await database.Assets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                candidate => candidate.Id != asset.Id
                    && candidate.NormalizedManufacturer == asset.NormalizedManufacturer
                    && candidate.NormalizedSerialNumber == asset.NormalizedSerialNumber,
                cancellationToken)
            .ConfigureAwait(false);

        return taken
            ? AssetsErrors.DuplicateSerialNumber(asset.Manufacturer!, asset.SerialNumber!)
            : null;
    }

    /// <summary>
    /// Reads the type the edit names, refusing a retired one only when it is a change.
    /// </summary>
    private async Task<(AssetType? Type, Error? Failure)> ResolveTypeAsync(
        Asset asset,
        Guid assetTypeId,
        CancellationToken cancellationToken)
    {
        var type = await database.AssetTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == assetTypeId, cancellationToken)
            .ConfigureAwait(false);

        if (type is null)
        {
            return (null, AssetsErrors.AssetTypeNotFound());
        }

        // Retiring a type must not freeze the equipment already classified as one. Keeping
        // the type the asset already has is therefore always allowed; moving into a retired
        // type is not.
        return type.IsActive || type.Id == asset.AssetTypeId
            ? (type, null)
            : (null, AssetsErrors.AssetTypeRetired());
    }

    /// <summary>
    /// Resolves the department and location through their owning module's contracts, and
    /// caches their display strings per §3 rule 6.
    /// </summary>
    /// <remarks>
    /// <b>This is where a rename catches up.</b> Nothing refreshes an asset's cached
    /// <c>department_name</c> or <c>location_path</c> — the gap STATUS.md has carried since
    /// WP-2.3 — but an edit reads both strings fresh, so saving an asset's form brings its
    /// two cached names back into agreement with Directory. That is a side effect of the
    /// edit rather than the fix, and the refresh-consumer package is still owed.
    /// </remarks>
    private async Task<(Guid? DepartmentId, string? DepartmentName, Guid? LocationId, string? LocationPath, Error? Failure)>
        ResolvePlacementAsync(UpdateAssetRequest request, CancellationToken cancellationToken)
    {
        Guid? departmentId = null;
        string? departmentName = null;
        Guid? locationId = null;
        string? locationPath = null;

        if (request.DepartmentId is { } wantedDepartment)
        {
            var department = await departments.GetAsync(wantedDepartment, cancellationToken).ConfigureAwait(false);

            if (department is null)
            {
                return (null, null, null, null, AssetsErrors.DepartmentNotFound());
            }

            // A retired department is accepted, as on the create: retiring a department does
            // not move the equipment sitting in it, and refusing would make an asset in a
            // dissolved department uneditable — which is precisely the asset somebody is
            // trying to correct.
            departmentId = department.Id;
            departmentName = department.Name;
        }

        if (request.LocationId is { } wantedLocation)
        {
            var location = await locations.GetAsync(wantedLocation, cancellationToken).ConfigureAwait(false);

            if (location is null)
            {
                return (null, null, null, null, AssetsErrors.LocationNotFound());
            }

            locationId = location.Id;
            locationPath = location.Path;
        }

        return (departmentId, departmentName, locationId, locationPath, null);
    }
}
