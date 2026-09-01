using Itms.Modules.Assets.Domain;
using Itms.Platform.Results;

namespace Itms.Modules.Assets.Features.Assets.RetireAsset;

/// <summary>Takes an asset out of service.</summary>
/// <remarks>
/// <b>Retiring deployed equipment moves two dimensions</b> — it releases the holder as well
/// as changing the status — so it writes two history entries at one instant and raises both
/// events. Retired is terminal, so this is the last lifecycle operation an asset accepts.
/// </remarks>
/// <param name="mutation">The shared transaction envelope every lifecycle operation runs in.</param>
internal sealed class RetireAssetHandler(AssetLifecycleMutation mutation)
{
    /// <summary>Retires the asset.</summary>
    /// <param name="assetId">The asset being retired.</param>
    /// <param name="request">The operator's note, if any.</param>
    /// <param name="expectedVersions">The caller's <c>If-Match</c> precondition, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>The asset as it now stands, or the failure that stopped the move.</returns>
    public Task<Result<AssetDetail>> HandleAsync(
        Guid assetId,
        AssetLifecycleRequest request,
        IReadOnlySet<uint>? expectedVersions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return mutation.ApplyAsync(
            assetId,
            expectedVersions,
            request.Note,
            [AssetStatusCode.Retired],
            context => context.Asset.Retire(
                context.Current,
                context.Status(AssetStatusCode.Retired),
                context.Now,
                context.Actor),
            cancellationToken);
    }
}
