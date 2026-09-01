using Itms.Contracts.Events;
using Itms.Contracts.Messaging;
using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Features.AssetHistory;
using Itms.Modules.Assets.Persistence;
using Itms.Modules.Assets.Persistence.Configurations;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.Assets;

/// <summary>
/// Everything one lifecycle operation is given to work with: the asset, where it stands,
/// where it may go, when, and at whose hand.
/// </summary>
/// <remarks>
/// Passed as one value rather than five parameters so that adding something a future
/// operation needs does not rewrite the signature of every one that does not. The actor
/// comes from here rather than from the operation's own closure, so no call site can
/// attribute a change to somebody else by accident — the same reason
/// <c>AssetHistoryRecorder</c> reads it from <c>ICurrentUser</c>.
/// </remarks>
/// <param name="Asset">The asset, loaded and tracked inside the transaction.</param>
/// <param name="Current">The status it carries right now, resolved by id.</param>
/// <param name="Statuses">The active lifecycle statuses this operation asked for, by code.</param>
/// <param name="Now">The instant the change is happening, from <c>IClock</c>.</param>
/// <param name="Actor">Who is making the request, or <see langword="null"/> for the system.</param>
internal readonly record struct AssetLifecycleContext(
    Asset Asset,
    AssetStatusRef Current,
    IReadOnlyDictionary<string, AssetStatusRef> Statuses,
    DateTimeOffset Now,
    Guid? Actor)
{
    /// <summary>
    /// The active status carrying <paramref name="code"/>, or <see langword="null"/> when
    /// this deployment has none.
    /// </summary>
    /// <remarks>
    /// Not <c>GetValueOrDefault</c>: <see cref="AssetStatusRef"/> is a struct, so a missing
    /// code would come back as a status with an empty id rather than as nothing, and the
    /// entity would move the asset into it. The entity methods take a nullable ref and
    /// answer <c>MissingLifecycleStatus</c> when they needed one they did not get.
    /// </remarks>
    /// <param name="code">The status code wanted.</param>
    /// <returns>The status, or <see langword="null"/>.</returns>
    public AssetStatusRef? Status(string code) =>
        Statuses.TryGetValue(code, out var status) ? status : null;
}

/// <summary>
/// The transaction every asset lifecycle operation runs inside: load, check the caller's
/// precondition, apply, record the history, save, announce.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why one envelope rather than four handlers that each remember all of it.</b>
/// Assignment, repair, return to service and retirement differ in one line — which method
/// on <see cref="Asset"/> they call — and agree on everything that is easy to get wrong:
/// taking the snapshot before the change, writing the history inside the same transaction
/// as the change (invariant 5), reading the row version back after the write rather than
/// before it, and publishing the two events ARCHITECTURE.md §5 names. Written out four
/// times, the fourth copy is where one of those quietly goes missing. WP-1.6 had to go
/// back and delete a duplicated audit write from a Helpdesk handler for exactly this
/// reason.
/// </para>
/// <para>
/// <b>Which events go out is derived, not declared.</b> The caller does not say what it
/// changed; this compares the snapshot with the asset afterwards, exactly as
/// <see cref="AssetChanges.Between"/> derives the history entries from the same pair. So a
/// transfer raises <c>AssetAssigned</c> alone, a repair raises <c>AssetStatusChanged</c>
/// alone, and issuing equipment out of stock raises both — and no operation can announce a
/// change it did not make, or fail to announce one it did.
/// </para>
/// <para>
/// <b>Nothing here writes an audit row, and that is the point.</b> The Audit module has
/// bound a consumer to both events since WP-0.7, so the trail is derived from them. Adding
/// an <c>IAuditWriter</c> call beside the publish would record every assignment and every
/// lifecycle move twice — the trap <c>AssetsAudit</c> has carried a warning about since
/// WP-2.1, and the one WP-1.6 had to defuse in Helpdesk.
/// </para>
/// </remarks>
/// <param name="database">The assets context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock. Every instant this writes comes from here.</param>
/// <param name="currentUser">Who is making the request.</param>
/// <param name="history">The asset's own timeline (invariant 5, SPEC.md §3).</param>
/// <param name="publisher">The outbox, enrolled in this operation's own transaction.</param>
internal sealed class AssetLifecycleMutation(
    AssetsDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    AssetHistoryRecorder history,
    IEventPublisher publisher)
{
    /// <summary>Runs one lifecycle operation against one asset.</summary>
    /// <param name="assetId">The asset being moved.</param>
    /// <param name="expectedVersions">
    /// The row versions the caller's <c>If-Match</c> will accept, or <see langword="null"/>
    /// when it stated no precondition.
    /// </param>
    /// <param name="note">What the operator said about it, written onto every entry the operation produces.</param>
    /// <param name="lifecycleCodes">
    /// The status codes this operation may need to move the asset into. Resolved to active
    /// rows in the same query as the asset's current status; a code the deployment does not
    /// have simply arrives missing, and the entity returns
    /// <c>AssetsErrors.MissingLifecycleStatus</c> if it actually needed it.
    /// </param>
    /// <param name="apply">The entity method to call, given everything it needs.</param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>The asset as it now stands, or the failure that stopped the operation.</returns>
    public async Task<Result<AssetDetail>> ApplyAsync(
        Guid assetId,
        IReadOnlySet<uint>? expectedVersions,
        string? note,
        string[] lifecycleCodes,
        Func<AssetLifecycleContext, Result> apply,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lifecycleCodes);
        ArgumentNullException.ThrowIfNull(apply);

        Error? failure = null;
        AssetDetail? changed = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                // Tracked, not AsNoTracking: this is a write, and the xmin token WP-2.1
                // mapped only does its job on a tracked entity. The soft-delete query
                // filter applies, so a deleted asset is a 404 rather than a silent write.
                var asset = await database.Assets
                    .FirstOrDefaultAsync(candidate => candidate.Id == assetId, token)
                    .ConfigureAwait(false);

                if (asset is null)
                {
                    failure = AssetsErrors.AssetNotFound();
                    return;
                }

                var entry = database.Entry(asset);

                // The caller's precondition, checked before anything is attempted — the
                // whole point of the 412. The row is already loaded and locked by the read,
                // so this cannot itself race.
                if (expectedVersions is not null
                    && !expectedVersions.Contains(entry.Property<uint>(AssetConfiguration.VersionProperty).CurrentValue))
                {
                    failure = AssetsErrors.AssetPreconditionFailed();
                    return;
                }

                var (current, byCode) = await ResolveStatusesAsync(asset, lifecycleCodes, token).ConfigureAwait(false);

                // Taken before the move: the recorder works out which entries the operation
                // owes by comparing this with the asset afterwards, so no handler names its
                // own history lines.
                var before = AssetSnapshot.Of(asset, current);
                var now = clock.UtcNow;

                var moved = apply(new AssetLifecycleContext(asset, current, byCode, now, currentUser.UserId));

                if (moved.IsFailure)
                {
                    failure = moved.Error;
                    return;
                }

                var after = StatusNow(asset, current, byCode);

                // Added, not saved: the entries reach the database on the SaveChanges
                // below, inside this transaction. That is invariant 5.
                history.Record(asset, before, after, now, note);

                try
                {
                    await database.SaveChangesAsync(token).ConfigureAwait(false);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Somebody moved the asset between the read and the write. A 409 the
                    // client can retry, not the 500 an unhandled one would be.
                    failure = AssetsErrors.AssetChangedConcurrently();
                    return;
                }

                await PublishAsync(asset, before, after, now, token).ConfigureAwait(false);

                var typeName = await database.AssetTypes
                    .AsNoTracking()
                    .Where(type => type.Id == asset.AssetTypeId)
                    .Select(type => type.Name)
                    .FirstAsync(token)
                    .ConfigureAwait(false);

                changed = new AssetDetail(
                    AssetResponse.From(asset, asset.AssetTypeId, typeName, after),
                    // Read back off the tracked entry rather than from before the write:
                    // xmin is ValueGeneratedOnAddOrUpdate, so EF returns the new value with
                    // the UPDATE and refreshes it here. A stale tag would be worse than no
                    // tag.
                    entry.Property<uint>(AssetConfiguration.VersionProperty).CurrentValue);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is null
            ? Result.Success(changed!)
            : Result.Failure<AssetDetail>(failure);
    }

    /// <summary>
    /// Reads the asset's current status and the active rows for the codes this operation
    /// might move it into, in one query.
    /// </summary>
    /// <remarks>
    /// The current status is fetched by id and not by code, because it may itself have been
    /// retired since the asset landed in it — an administrator can do that, and the asset
    /// does not stop being in it. The destinations are filtered to active rows for the
    /// opposite reason: moving an asset into a status somebody has retired would be
    /// re-opening it by the back door.
    /// </remarks>
    private async Task<(AssetStatusRef Current, IReadOnlyDictionary<string, AssetStatusRef> ByCode)> ResolveStatusesAsync(
        Asset asset,
        string[] lifecycleCodes,
        CancellationToken cancellationToken)
    {
        var rows = await database.AssetStatuses
            .AsNoTracking()
            .Where(status => status.Id == asset.AssetStatusId
                || (lifecycleCodes.Contains(status.Code) && status.IsActive))
            .Select(status => new { status.Id, status.Code, status.Name, status.IsActive })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var currentRow = rows.Find(row => row.Id == asset.AssetStatusId)
            // fk_assets_asset_status_id is RESTRICT and there is no delete path for a
            // status, so the row cannot be missing. If it ever is, the module's own
            // referential integrity has gone and a 500 is the honest answer.
            ?? throw new InvalidOperationException(
                $"Asset {asset.Id} carries status {asset.AssetStatusId}, which does not exist.");

        var byCode = rows
            .Where(row => row.IsActive)
            .ToDictionary(
                row => row.Code,
                row => new AssetStatusRef(row.Id, row.Code, row.Name),
                StringComparer.Ordinal);

        return (new AssetStatusRef(currentRow.Id, currentRow.Code, currentRow.Name), byCode);
    }

    /// <summary>
    /// The status the asset carries after the operation, without going back to the
    /// database for a row the caller already resolved.
    /// </summary>
    private static AssetStatusRef StatusNow(
        Asset asset,
        AssetStatusRef current,
        IReadOnlyDictionary<string, AssetStatusRef> byCode)
    {
        if (asset.AssetStatusId == current.Id)
        {
            return current;
        }

        // The entity can only have moved into one of the statuses it was handed, so the
        // destination is always among them.
        foreach (var candidate in byCode.Values)
        {
            if (candidate.Id == asset.AssetStatusId)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Asset {asset.Id} moved to status {asset.AssetStatusId}, which was not one of the resolved destinations.");
    }

    /// <summary>
    /// Stages the facts this operation produced into the transaction that produced them.
    /// </summary>
    /// <remarks>
    /// Each event goes out only when its own dimension actually moved. Publishing
    /// <c>AssetStatusChanged</c> on a transfer would put a row in the audit trail saying an
    /// asset went from Deployed to Deployed, which is the mistake WP-1.6 documented for a
    /// reassignment that moves no status.
    /// </remarks>
    private async Task PublishAsync(
        Asset asset,
        AssetSnapshot before,
        AssetStatusRef after,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (before.AssignedToUserId != asset.AssignedToUserId)
        {
            await publisher.PublishAsync(
                new AssetAssigned(
                    asset.Id,
                    asset.AssetTag,
                    asset.AssignedToUserId,
                    before.AssignedToUserId)
                {
                    // Stamped explicitly: the dispatcher runs on a background scope with no
                    // principal, so the actor the audit trail records is the one named here.
                    ActorId = currentUser.UserId,
                    OccurredAt = now,
                },
                cancellationToken).ConfigureAwait(false);
        }

        if (before.StatusId == after.Id)
        {
            return;
        }

        // The codes, not the names. The audit trail is machine-readable and permanent, and
        // a display name an administrator can edit would make two entries written a year
        // apart describe the same move differently. The history entry is the opposite case
        // and records the names — see AssetHistoryEntry.
        await publisher.PublishAsync(
            new AssetStatusChanged(asset.Id, asset.AssetTag, before.StatusCode, after.Code)
            {
                ActorId = currentUser.UserId,
                OccurredAt = now,
            },
            cancellationToken).ConfigureAwait(false);
    }
}
