using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.AssetStatuses.GetAssetStatus;

/// <summary>Reads one asset status.</summary>
/// <param name="database">The assets context.</param>
internal sealed class GetAssetStatusHandler(AssetsDbContext database)
{
    /// <summary>Reads the status with <paramref name="assetStatusId"/>.</summary>
    /// <param name="assetStatusId">The status to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The status, or a not-found failure.</returns>
    public async Task<Result<AssetStatusResponse>> HandleAsync(Guid assetStatusId, CancellationToken cancellationToken)
    {
        var status = await database.AssetStatuses
            .AsNoTracking()
            .Where(candidate => candidate.Id == assetStatusId)
            .Select(AssetStatusResponse.Projection())
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return status is null ? AssetsErrors.AssetStatusNotFound() : status;
    }
}
