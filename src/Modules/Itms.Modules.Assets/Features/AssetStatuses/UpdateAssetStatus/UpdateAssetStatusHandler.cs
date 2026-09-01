using System.Globalization;
using Itms.Contracts.Auditing;
using Itms.Modules.Assets.Auditing;
using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.AssetStatuses.UpdateAssetStatus;

/// <summary>
/// Edits an asset status's name, description, and order. The code does not move.
/// </summary>
/// <param name="database">The assets context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
internal sealed class UpdateAssetStatusHandler(
    AssetsDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit)
{
    /// <summary>Applies <paramref name="request"/> to the status with <paramref name="assetStatusId"/>.</summary>
    /// <param name="assetStatusId">The status to edit.</param>
    /// <param name="request">The new field values.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The edited status, a not-found, or a conflict on a duplicate name.</returns>
    public async Task<Result<AssetStatusResponse>> HandleAsync(
        Guid assetStatusId,
        UpdateAssetStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Error? failure = null;
        AssetStatusResponse? updated = null;

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

                // Read before the entity mutates: the diff is the whole point of the entry.
                var previousName = status.Name;
                var previousDescription = status.Description;
                var previousSortOrder = status.SortOrder;

                var now = clock.UtcNow;
                var actor = currentUser.UserId;

                status.Rename(request.Name, now, actor);
                status.Describe(request.Description, now, actor);
                status.Reorder(request.SortOrder, now, actor);

                // Run after the entity has normalised the input, so the check compares the
                // same string the unique index will. The code is immutable, so it is not
                // re-checked here.
                failure = await AssetStatusUniqueness
                    .CheckNameAsync(database, status.NormalizedName, assetStatusId, token)
                    .ConfigureAwait(false);

                if (failure is not null)
                {
                    return;
                }

                await database.SaveChangesAsync(token).ConfigureAwait(false);

                await audit.WriteAsync(
                    new AuditEntry(
                        AssetsAudit.AssetStatusUpdated,
                        AssetsAudit.AssetStatusEntityType,
                        status.Id.ToString(),
                        AssetsAudit.Changes()
                            .Moved("name", previousName, status.Name)
                            .Moved("description", previousDescription, status.Description)
                            .Moved(
                                "sortOrder",
                                previousSortOrder.ToString(CultureInfo.InvariantCulture),
                                status.SortOrder.ToString(CultureInfo.InvariantCulture))),
                    token).ConfigureAwait(false);

                updated = AssetStatusResponse.From(status);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? failure : updated!;
    }
}
