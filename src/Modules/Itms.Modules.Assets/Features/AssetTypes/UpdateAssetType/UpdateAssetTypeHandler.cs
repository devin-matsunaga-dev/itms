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

namespace Itms.Modules.Assets.Features.AssetTypes.UpdateAssetType;

/// <summary>
/// Edits an asset type's name, description, and order.
/// </summary>
/// <remarks>
/// A rename touches this row and nothing else. Assets hold the type's id, so every asset
/// already classified under it reads the new name from its next query — which is the whole
/// reason the name is not copied onto an asset in the first place.
/// </remarks>
/// <param name="database">The assets context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
internal sealed class UpdateAssetTypeHandler(
    AssetsDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit)
{
    /// <summary>Applies <paramref name="request"/> to the type with <paramref name="assetTypeId"/>.</summary>
    /// <param name="assetTypeId">The type to edit.</param>
    /// <param name="request">The new field values.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The edited type, a not-found, or a conflict on a duplicate name.</returns>
    public async Task<Result<AssetTypeResponse>> HandleAsync(
        Guid assetTypeId,
        UpdateAssetTypeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Error? failure = null;
        AssetTypeResponse? updated = null;

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

                // Read before the entity mutates: the diff is the whole point of the entry,
                // and after Rename there is nothing left to compare against.
                var previousName = type.Name;
                var previousDescription = type.Description;
                var previousSortOrder = type.SortOrder;

                var now = clock.UtcNow;
                var actor = currentUser.UserId;

                type.Rename(request.Name, now, actor);
                type.Describe(request.Description, now, actor);
                type.Reorder(request.SortOrder, now, actor);

                // Run after the entity has normalised the input, so the check compares the
                // same string the unique index will.
                failure = await AssetTypeUniqueness
                    .CheckAsync(database, type.NormalizedName, assetTypeId, token)
                    .ConfigureAwait(false);

                if (failure is not null)
                {
                    return;
                }

                await database.SaveChangesAsync(token).ConfigureAwait(false);

                await audit.WriteAsync(
                    new AuditEntry(
                        AssetsAudit.AssetTypeUpdated,
                        AssetsAudit.AssetTypeEntityType,
                        type.Id.ToString(),
                        AssetsAudit.Changes()
                            .Moved("name", previousName, type.Name)
                            .Moved("description", previousDescription, type.Description)
                            .Moved(
                                "sortOrder",
                                previousSortOrder.ToString(CultureInfo.InvariantCulture),
                                type.SortOrder.ToString(CultureInfo.InvariantCulture))),
                    token).ConfigureAwait(false);

                updated = AssetTypeResponse.From(type);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? failure : updated!;
    }
}
