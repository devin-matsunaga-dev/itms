using Itms.Modules.Assets.Domain;
using Itms.Platform.Results;

namespace Itms.Modules.Assets.Features.Assets.ReturnAssetToService;

/// <summary>Brings an asset back from repair.</summary>
/// <remarks>
/// Where it lands is the entity's call and depends on whether anybody still holds it —
/// deployed if somebody does, in stock if nobody does. Both destinations are asked for
/// here because which one is needed is not known until the transaction has read the asset.
/// </remarks>
/// <param name="mutation">The shared transaction envelope every lifecycle operation runs in.</param>
internal sealed class ReturnAssetToServiceHandler(AssetLifecycleMutation mutation)
{
    /// <summary>Moves the asset out of repair.</summary>
    /// <param name="assetId">The asset coming back.</param>
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
            [AssetStatusCode.Deployed, AssetStatusCode.InStock],
            context => context.Asset.ReturnToService(
                context.Current,
                context.Status(AssetStatusCode.Deployed),
                context.Status(AssetStatusCode.InStock),
                context.Now,
                context.Actor),
            cancellationToken);
    }
}
