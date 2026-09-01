using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.AssetTypes.GetAssetType;

/// <summary>Reads one asset type.</summary>
/// <param name="database">The assets context.</param>
internal sealed class GetAssetTypeHandler(AssetsDbContext database)
{
    /// <summary>Reads the type with <paramref name="assetTypeId"/>.</summary>
    /// <param name="assetTypeId">The type to read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The type, or a not-found failure.</returns>
    public async Task<Result<AssetTypeResponse>> HandleAsync(Guid assetTypeId, CancellationToken cancellationToken)
    {
        var type = await database.AssetTypes
            .AsNoTracking()
            .Where(candidate => candidate.Id == assetTypeId)
            .Select(AssetTypeResponse.Projection())
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return type is null ? AssetsErrors.AssetTypeNotFound() : type;
    }
}
