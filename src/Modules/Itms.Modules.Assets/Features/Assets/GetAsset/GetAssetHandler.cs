using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Itms.Modules.Assets.Persistence.Configurations;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.Assets.GetAsset;

/// <summary>
/// Reads one asset.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is in WP-2.1 and not WP-2.3.</b> The create endpoint answers 201 with a
/// <c>Location</c> header, and a header pointing at a route nothing serves is not a
/// finished create endpoint — it also leaves the 409 criterion assertable only against the
/// creating request's own body. WP-2.3 owns the list: filtering, search, paging, and
/// sorting, which is the substantial half and is untouched here.
/// </para>
/// <para>
/// <b>It answers with the row version as well</b>, which WP-2.2 added when the asset got
/// its first mutation: the endpoint turns it into an <c>ETag</c>, and without a read that
/// carries one there is nothing for a client to put in the <c>If-Match</c> the lifecycle
/// routes honour.
/// </para>
/// <para>
/// The join is explicit because the type and status foreign keys carry no navigation
/// property — <c>AssetConfiguration</c> maps them with <c>HasOne&lt;T&gt;().WithMany()</c>
/// so that nothing can accidentally load an aggregate to render a row. Projected in the
/// query, never by loading entities and mapping them after (CONVENTIONS.md).
/// </para>
/// </remarks>
/// <param name="database">The assets context.</param>
internal sealed class GetAssetHandler(AssetsDbContext database)
{
    /// <summary>Reads the asset with <paramref name="assetId"/>.</summary>
    /// <param name="assetId">The asset to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The asset and its version, or a not-found failure. A soft-deleted asset is not found.</returns>
    public async Task<Result<AssetDetail>> HandleAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var asset = await (
            from candidate in database.Assets.AsNoTracking()
            join type in database.AssetTypes.AsNoTracking() on candidate.AssetTypeId equals type.Id
            join status in database.AssetStatuses.AsNoTracking() on candidate.AssetStatusId equals status.Id
            where candidate.Id == assetId
            select new AssetDetail(
                new AssetResponse(
                    candidate.Id,
                    candidate.AssetTag,
                    candidate.Name,
                    candidate.SerialNumber,
                    candidate.Barcode,
                    candidate.Manufacturer,
                    candidate.Model,
                    type.Id,
                    type.Name,
                    status.Id,
                    status.Code,
                    status.Name,
                    candidate.AssignedToUserId,
                    candidate.AssignedToUserName,
                    candidate.DepartmentId,
                    candidate.DepartmentName,
                    candidate.LocationId,
                    candidate.LocationPath,
                    candidate.PurchaseDate,
                    candidate.WarrantyExpiresAt,
                    candidate.Vendor,
                    candidate.Cost,
                    candidate.Notes,
                    candidate.CreatedAt,
                    candidate.UpdatedAt),
                EF.Property<uint>(candidate, AssetConfiguration.VersionProperty)))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return asset is null ? AssetsErrors.AssetNotFound() : asset;
    }
}
