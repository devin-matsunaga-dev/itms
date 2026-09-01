using System.Globalization;
using Itms.Contracts.Auditing;
using Itms.Modules.Assets.Auditing;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;

namespace Itms.Modules.Assets.Features.AssetStatuses.CreateAssetStatus;

/// <summary>Creates an asset status.</summary>
/// <param name="database">The assets context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock.</param>
/// <param name="currentUser">Who is making the request, for the audit columns.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
internal sealed class CreateAssetStatusHandler(
    AssetsDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit)
{
    /// <summary>Creates the status described by <paramref name="request"/>.</summary>
    /// <param name="request">The new status's fields.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The created status, or a conflict on a duplicate name or code.</returns>
    public async Task<Result<AssetStatusResponse>> HandleAsync(
        CreateAssetStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var status = Domain.AssetStatus.Create(
            request.Code,
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

                failure = await AssetStatusUniqueness
                    .CheckNameAsync(database, status.NormalizedName, excluding: null, token)
                    .ConfigureAwait(false);

                failure ??= await AssetStatusUniqueness
                    .CheckCodeAsync(database, status.Code, token)
                    .ConfigureAwait(false);

                if (failure is not null)
                {
                    return;
                }

                database.AssetStatuses.Add(status);
                await database.SaveChangesAsync(token).ConfigureAwait(false);

                // Inside the transaction, so a create that rolls back leaves no entry
                // claiming it happened.
                await audit.WriteAsync(
                    new AuditEntry(
                        AssetsAudit.AssetStatusCreated,
                        AssetsAudit.AssetStatusEntityType,
                        status.Id.ToString(),
                        AssetsAudit.Changes()
                            .Set("code", status.Code)
                            .Set("name", status.Name)
                            .Set("description", status.Description)
                            .Set("sortOrder", status.SortOrder.ToString(CultureInfo.InvariantCulture))),
                    token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is not null ? failure : AssetStatusResponse.From(status);
    }
}
