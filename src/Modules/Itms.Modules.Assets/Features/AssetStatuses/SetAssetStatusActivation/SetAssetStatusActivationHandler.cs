using Itms.Contracts.Auditing;
using Itms.Modules.Assets.Auditing;
using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.AssetStatuses.SetAssetStatusActivation;

/// <summary>
/// Retires an asset status or brings it back.
/// </summary>
/// <remarks>
/// This is what stands in for a delete, and there is no delete.
/// <c>fk_assets_asset_status_id</c> is <c>ON DELETE RESTRICT</c>, so the database refuses
/// as well as the API, and every historical asset keeps resolving the status it is in.
/// <para>
/// <b>Retiring the seeded <c>in-stock</c> status is allowed and has a consequence:</b>
/// creating an asset without naming a status then fails with
/// <c>assets.no_default_asset_status</c> rather than silently picking another. That is
/// deliberate — guessing which of the remaining statuses a new asset belongs in would be
/// inventing policy.
/// </para>
/// </remarks>
/// <param name="database">The assets context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
internal sealed class SetAssetStatusActivationHandler(
    AssetsDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit)
{
    /// <summary>Sets whether the status is active.</summary>
    /// <param name="assetStatusId">The status to change.</param>
    /// <param name="isActive">True to reinstate it, false to retire it.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success, or a not-found failure. Setting the state it already has succeeds.</returns>
    public async Task<Result> HandleAsync(Guid assetStatusId, bool isActive, CancellationToken cancellationToken)
    {
        Error? failure = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                var status = await database.AssetStatuses
                    .FirstOrDefaultAsync(candidate => candidate.Id == assetStatusId, token)
                    .ConfigureAwait(false);

                if (status is null)
                {
                    failure = AssetsErrors.AssetStatusNotFound();
                    return;
                }

                var wasActive = status.IsActive;
                var now = clock.UtcNow;
                var actor = currentUser.UserId;

                if (isActive)
                {
                    status.Reactivate(now, actor);
                }
                else
                {
                    status.Deactivate(now, actor);
                }

                await database.SaveChangesAsync(token).ConfigureAwait(false);

                // Setting the state it already has is a success, not a change. Auditing it
                // would fill the trail with entries in which nothing moved.
                if (wasActive != status.IsActive)
                {
                    await audit.WriteAsync(
                        new AuditEntry(
                            status.IsActive
                                ? AssetsAudit.AssetStatusReinstated
                                : AssetsAudit.AssetStatusRetired,
                            AssetsAudit.AssetStatusEntityType,
                            status.Id.ToString(),
                            AssetsAudit.Changes().Moved(
                                "isActive",
                                wasActive ? "true" : "false",
                                status.IsActive ? "true" : "false")),
                        token).ConfigureAwait(false);
                }
            },
            cancellationToken).ConfigureAwait(false);

        return failure is null ? Result.Success() : Result.Failure(failure);
    }
}
