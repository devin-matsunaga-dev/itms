using System.Globalization;
using Itms.Contracts.Auditing;
using Itms.Modules.Assets.Auditing;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;

namespace Itms.Modules.Assets.Features.AssetTypes.CreateAssetType;

/// <summary>Creates an asset type.</summary>
/// <param name="database">The assets context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
internal sealed class CreateAssetTypeHandler(
    AssetsDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit)
{
    /// <summary>Creates the type described by <paramref name="request"/>.</summary>
    /// <param name="request">The new type's fields.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The created type, or a conflict on a duplicate name.</returns>
    public async Task<Result<AssetTypeResponse>> HandleAsync(
        CreateAssetTypeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var type = Domain.AssetType.Create(
            request.Name,
            request.Description,
            request.SortOrder,
            clock.UtcNow,
            currentUser.UserId);

        Error? failure = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                failure = await AssetTypeUniqueness
                    .CheckAsync(database, type.NormalizedName, excluding: null, token)
                    .ConfigureAwait(false);

                if (failure is not null)
                {
                    return;
                }

                database.AssetTypes.Add(type);
                await database.SaveChangesAsync(token).ConfigureAwait(false);

                // Inside the transaction, so a create that rolls back leaves no entry
                // claiming it happened.
                await audit.WriteAsync(
                    new AuditEntry(
                        AssetsAudit.AssetTypeCreated,
                        AssetsAudit.AssetTypeEntityType,
                        type.Id.ToString(),
                        AssetsAudit.Changes()
                            .Set("name", type.Name)
                            .Set("description", type.Description)
                            .Set("sortOrder", type.SortOrder.ToString(CultureInfo.InvariantCulture))),
                    token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? failure : AssetTypeResponse.From(type);
    }
}
