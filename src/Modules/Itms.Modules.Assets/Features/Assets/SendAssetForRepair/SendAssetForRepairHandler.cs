using Itms.Modules.Assets.Domain;
using Itms.Platform.Results;

namespace Itms.Modules.Assets.Features.Assets.SendAssetForRepair;

/// <summary>Sends an asset away to be fixed.</summary>
/// <remarks>
/// <b>The holder is deliberately kept.</b> A laptop at the vendor is still the machine
/// issued to whoever had it, so this moves one dimension and writes one history entry.
/// It is also what lets <c>ReturnAssetToServiceHandler</c> know where the asset goes back
/// to when it comes home.
/// </remarks>
/// <param name="mutation">The shared transaction envelope every lifecycle operation runs in.</param>
internal sealed class SendAssetForRepairHandler(AssetLifecycleMutation mutation)
{
    /// <summary>Moves the asset into repair.</summary>
    /// <param name="assetId">The asset going away.</param>
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
            [AssetStatusCode.Repair],
            context => context.Asset.SendForRepair(
                context.Current,
                context.Status(AssetStatusCode.Repair),
                context.Now,
                context.Actor),
            cancellationToken);
    }
}
