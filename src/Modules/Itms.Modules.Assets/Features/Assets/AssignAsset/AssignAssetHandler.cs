using Itms.Contracts.Lookups;
using Itms.Modules.Assets.Domain;
using Itms.Platform.Results;

namespace Itms.Modules.Assets.Features.Assets.AssignAsset;

/// <summary>
/// Issues an asset to somebody, hands it from one person to another, or takes it back.
/// </summary>
/// <remarks>
/// <para>
/// <b>The handler decides nothing about whether the move is allowed.</b>
/// <see cref="Asset.AssignTo"/> and <see cref="Asset.Return"/> do, and this reads the
/// answer — the division WP-1.3 drew for a ticket's status change, for the same reason: the
/// rule that retired equipment holds nobody has to live where it cannot be routed around.
/// </para>
/// <para>
/// <b>Who may hold an asset is the one question the entity cannot answer.</b> Whether an
/// account exists and is active are facts about Identity's rows, so they are read here
/// through <see cref="IUserLookup"/> and the name is cached onto the asset per §3 rule 6.
/// <b>No role is required</b>, unlike a ticket assignee: WP-1.6 refused a ticket to anybody
/// but a technician because an end user has no route to work a queue, but equipment is
/// issued to end users — that is what equipment is for. The Technician policy on the
/// endpoint is about who may <em>make</em> the assignment, not who may receive it.
/// </para>
/// <para>
/// A transfer is this handler against an asset somebody already holds. It produces exactly
/// one history entry, carrying both parties, because only one dimension moved — which is
/// WP-2.2's done-criterion, and it is true by construction rather than by this handler
/// remembering it.
/// </para>
/// </remarks>
/// <param name="mutation">The shared transaction envelope every lifecycle operation runs in.</param>
/// <param name="users">Identity's public contract, for the holder's name and active state.</param>
internal sealed class AssignAssetHandler(AssetLifecycleMutation mutation, IUserLookup users)
{
    /// <summary>Applies <paramref name="request"/> to the asset.</summary>
    /// <param name="assetId">The asset whose holder is changing.</param>
    /// <param name="request">Who is taking it on, or null to take it back.</param>
    /// <param name="expectedVersions">
    /// The row versions the caller's <c>If-Match</c> will accept, or <see langword="null"/>
    /// when it stated no precondition.
    /// </param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>The asset as it now stands, or the failure that stopped the change.</returns>
    public async Task<Result<AssetDetail>> HandleAsync(
        Guid assetId,
        AssignAssetRequest request,
        IReadOnlySet<uint>? expectedVersions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Read before the transaction opens: this is a cross-module read, and holding a row
        // lock on the asset across it would serialise every assignment behind Identity.
        var holder = await ResolveHolderAsync(request.AssignedToUserId, cancellationToken).ConfigureAwait(false);

        if (holder.IsFailure)
        {
            return Result.Failure<AssetDetail>(holder.Error!);
        }

        // Both destinations are named whichever way this goes, because which one is needed
        // depends on the status the asset turns out to be in and that is not known until the
        // transaction has read it.
        return await mutation.ApplyAsync(
            assetId,
            expectedVersions,
            request.Note,
            [AssetStatusCode.Deployed, AssetStatusCode.InStock],
            context => holder.Value is { } user
                ? context.Asset.AssignTo(
                    user.Id,
                    user.DisplayName,
                    context.Current,
                    context.Status(AssetStatusCode.Deployed),
                    context.Now,
                    context.Actor)
                : context.Asset.Return(
                    context.Current,
                    context.Status(AssetStatusCode.InStock),
                    context.Now,
                    context.Actor),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Finds the account the asset is being handed to, or establishes that it is being
    /// handed back.
    /// </summary>
    /// <remarks>
    /// A deactivated account is refused. Invariant 9 keeps the equipment a departed user
    /// already holds on their record — that history is theirs — but issuing them more is
    /// recording something that did not happen.
    /// </remarks>
    /// <param name="userId">The account named in the request, or null to take the asset back.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The holder, null for a return, or the failure that refuses it.</returns>
    private async Task<Result<UserSummary?>> ResolveHolderAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is not { } id)
        {
            return Result.Success<UserSummary?>(null);
        }

        var user = await users.GetAsync(id, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            return Result.Failure<UserSummary?>(AssetsErrors.HolderNotFound());
        }

        return user.IsActive
            ? Result.Success<UserSummary?>(user)
            : Result.Failure<UserSummary?>(AssetsErrors.HolderInactive());
    }
}
