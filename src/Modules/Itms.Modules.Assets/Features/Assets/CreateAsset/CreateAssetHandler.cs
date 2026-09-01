using System.Globalization;
using Itms.Contracts.Auditing;
using Itms.Contracts.Lookups;
using Itms.Modules.Assets.Auditing;
using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.Assets.CreateAsset;

/// <summary>
/// Records a new asset.
/// </summary>
/// <remarks>
/// <para>
/// The two uniqueness checks are what WP-2.1's done-criterion asks for. Both are advisory
/// — a concurrent insert can still beat them to the unique index — and both exist so the
/// common case comes back as a 409 naming the tag rather than as a database exception. The
/// indexes behind them are what make the rare case safe.
/// </para>
/// <para>
/// This is the first code in the module to read across a module boundary through
/// <c>Itms.Contracts</c>, and <b>the first consumer of <c>ILocationLookup</c> anywhere in
/// the system</b> — STATUS.md has recorded it as implemented but unused since WP-0.6. The
/// department's name and the location's path are copied onto the asset row because §3 rule
/// 6 forbids the foreign key and requires an id plus a cached display string instead.
/// Nothing refreshes those copies; see STATUS.md, where the gap is the same one three
/// cached names on a ticket have.
/// </para>
/// </remarks>
/// <param name="database">The assets context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
/// <param name="departments">How this module reads Directory's departments.</param>
/// <param name="locations">How this module reads Directory's locations.</param>
internal sealed class CreateAssetHandler(
    AssetsDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IDepartmentLookup departments,
    ILocationLookup locations)
{
    /// <summary>Records the asset described by <paramref name="request"/>.</summary>
    /// <param name="request">The new asset's fields.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The created asset, or a validation failure, or a 409 on a duplicate tag or serial.</returns>
    public async Task<Result<AssetResponse>> HandleAsync(
        CreateAssetRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Error? failure = null;
        AssetResponse? created = null;

        // Resolved before the transaction opens: these are reads against another module,
        // and holding a write transaction across them would widen it for no benefit.
        var placement = await ResolvePlacementAsync(request, cancellationToken).ConfigureAwait(false);

        if (placement.Failure is not null)
        {
            return placement.Failure;
        }

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                var type = await database.AssetTypes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(candidate => candidate.Id == request.AssetTypeId, token)
                    .ConfigureAwait(false);

                if (type is null)
                {
                    failure = AssetsErrors.AssetTypeNotFound();
                    return;
                }

                if (!type.IsActive)
                {
                    failure = AssetsErrors.AssetTypeRetired();
                    return;
                }

                var status = await ResolveStatusAsync(request.AssetStatusId, token).ConfigureAwait(false);

                if (status.Failure is not null)
                {
                    failure = status.Failure;
                    return;
                }

                var asset = Asset.Create(
                    new NewAsset(
                        request.AssetTag,
                        type.Id,
                        status.Status!.Id,
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

                failure = await CheckUniquenessAsync(asset, token).ConfigureAwait(false);

                if (failure is not null)
                {
                    return;
                }

                database.Assets.Add(asset);
                await database.SaveChangesAsync(token).ConfigureAwait(false);

                // Inside the transaction, so a create that rolls back leaves no entry
                // claiming it happened. SPEC.md §15 makes asset modifications mandatory
                // audit coverage; ARCHITECTURE.md §5 names no AssetCreated event, so this
                // goes through IAuditWriter — see AssetsAudit on why WP-2.2 must not add a
                // second route for the two events that do exist.
                await audit.WriteAsync(
                    new AuditEntry(
                        AssetsAudit.AssetCreated,
                        AssetsAudit.AssetEntityType,
                        asset.Id.ToString(),
                        AssetsAudit.Changes()
                            .Set("assetTag", asset.AssetTag)
                            .Set("assetTypeId", type.Id.ToString())
                            .Set("assetStatusId", status.Status.Id.ToString())
                            .Set("serialNumber", asset.SerialNumber)
                            .Set("manufacturer", asset.Manufacturer)
                            .Set("model", asset.Model)
                            .Set("departmentId", asset.DepartmentId?.ToString())
                            .Set("locationId", asset.LocationId?.ToString())
                            .Set("cost", asset.Cost?.ToString(CultureInfo.InvariantCulture))),
                    token).ConfigureAwait(false);

                created = AssetResponse.From(asset, type, status.Status);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? failure : created!;
    }

    /// <summary>
    /// Refuses a tag any other asset already carries, and a serial the same manufacturer
    /// has already used.
    /// </summary>
    private async Task<Error?> CheckUniquenessAsync(Asset asset, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters, deliberately: a soft-deleted asset keeps its tag reserved, so
        // the check has to see rows the ordinary filter hides. Without this the API would
        // answer "that tag is free" and the unique index would then refuse the insert with
        // a database exception nobody can act on.
        var tagTaken = await database.Assets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(candidate => candidate.NormalizedAssetTag == asset.NormalizedAssetTag, cancellationToken)
            .ConfigureAwait(false);

        if (tagTaken)
        {
            return AssetsErrors.DuplicateAssetTag(asset.AssetTag);
        }

        // "Unique per manufacturer where present": nothing to check unless both halves are
        // there, which is what the partial index says too.
        if (asset.NormalizedManufacturer is null || asset.NormalizedSerialNumber is null)
        {
            return null;
        }

        var serialTaken = await database.Assets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                candidate => candidate.NormalizedManufacturer == asset.NormalizedManufacturer
                    && candidate.NormalizedSerialNumber == asset.NormalizedSerialNumber,
                cancellationToken)
            .ConfigureAwait(false);

        return serialTaken
            ? AssetsErrors.DuplicateSerialNumber(asset.Manufacturer!, asset.SerialNumber!)
            : null;
    }

    /// <summary>
    /// Reads the named status, or falls back to the seeded <c>in-stock</c> row.
    /// </summary>
    private async Task<(AssetStatus? Status, Error? Failure)> ResolveStatusAsync(
        Guid? assetStatusId,
        CancellationToken cancellationToken)
    {
        if (assetStatusId is null)
        {
            var fallback = await database.AssetStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    candidate => candidate.Code == AssetStatusCode.InStock && candidate.IsActive,
                    cancellationToken)
                .ConfigureAwait(false);

            return fallback is null ? (null, AssetsErrors.NoDefaultAssetStatus()) : (fallback, null);
        }

        var status = await database.AssetStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == assetStatusId, cancellationToken)
            .ConfigureAwait(false);

        if (status is null)
        {
            return (null, AssetsErrors.AssetStatusNotFound());
        }

        return status.IsActive ? (status, null) : (null, AssetsErrors.AssetStatusRetired());
    }

    /// <summary>
    /// Resolves the department and location through their owning module's contracts, and
    /// caches their display strings per §3 rule 6.
    /// </summary>
    private async Task<(Guid? DepartmentId, string? DepartmentName, Guid? LocationId, string? LocationPath, Error? Failure)>
        ResolvePlacementAsync(CreateAssetRequest request, CancellationToken cancellationToken)
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

            // A retired department is accepted here, unlike a retired asset type. Retiring
            // a department does not move the equipment that sits in it, and refusing would
            // make an asset in a dissolved department unrecordable — which is precisely the
            // asset somebody is trying to find.
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
