using Itms.Contracts.Auditing;
using Itms.Modules.Assets.Auditing;
using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.AssetTypes.SetAssetTypeActivation;

/// <summary>
/// Retires an asset type or brings it back.
/// </summary>
/// <remarks>
/// <para>
/// This is what stands in for a delete, and there is no delete — the strongest form of "a
/// type in use cannot be removed" is a module with no removal path at all, which is also
/// what keeps every historical asset's classification readable.
/// <c>fk_assets_asset_type_id</c> is <c>ON DELETE RESTRICT</c>, so the database refuses as
/// well as the API.
/// </para>
/// <para>
/// Named <c>Activation</c> rather than following Helpdesk's <c>SetTicketCategoryStatus</c>,
/// because "status" is already an entity in this module and
/// <c>SetAssetStatusStatusHandler</c> would name two different things with one word.
/// </para>
/// </remarks>
/// <param name="database">The assets context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
internal sealed class SetAssetTypeActivationHandler(
    AssetsDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit)
{
    /// <summary>Sets whether the type is active.</summary>
    /// <param name="assetTypeId">The type to change.</param>
    /// <param name="isActive">True to reinstate it, false to retire it.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success, or a not-found failure. Setting the state it already has succeeds.</returns>
    public async Task<Result> HandleAsync(Guid assetTypeId, bool isActive, CancellationToken cancellationToken)
    {
        Error? failure = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                var type = await database.AssetTypes
                    .FirstOrDefaultAsync(candidate => candidate.Id == assetTypeId, token)
                    .ConfigureAwait(false);

                if (type is null)
                {
                    failure = AssetsErrors.AssetTypeNotFound();
                    return;
                }

                var wasActive = type.IsActive;
                var now = clock.UtcNow;
                var actor = currentUser.UserId;

                if (isActive)
                {
                    type.Reactivate(now, actor);
                }
                else
                {
                    type.Deactivate(now, actor);
                }

                await database.SaveChangesAsync(token).ConfigureAwait(false);

                // Setting the state it already has is a success, not a change. Auditing it
                // would fill the trail with entries in which nothing moved.
                if (wasActive != type.IsActive)
                {
                    await audit.WriteAsync(
                        new AuditEntry(
                            type.IsActive
                                ? AssetsAudit.AssetTypeReinstated
                                : AssetsAudit.AssetTypeRetired,
                            AssetsAudit.AssetTypeEntityType,
                            type.Id.ToString(),
                            AssetsAudit.Changes().Moved(
                                "isActive",
                                wasActive ? "true" : "false",
                                type.IsActive ? "true" : "false")),
                        token).ConfigureAwait(false);
                }
            },
            cancellationToken).ConfigureAwait(false);

        return failure is null ? Result.Success() : Result.Failure(failure);
    }
}
