using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.Assets.ListAssets;

/// <summary>Reads the asset list.</summary>
/// <remarks>
/// <para>
/// <b>Projected, never loaded.</b> CONVENTIONS.md forbids loading an aggregate to render a
/// list. The query joins to the type and status tables, orders, pages, and only then selects
/// into <see cref="AssetListItemResponse"/> — one SQL statement, no tracking, no lazy
/// loading, and no navigation property to walk, because <c>AssetConfiguration</c>
/// deliberately declares none.
/// </para>
/// <para>
/// <b>Soft-deleted assets are already gone</b> by the time this runs: WP-2.1 put the query
/// filter on the entity, which is why it was added before anything could set
/// <c>deleted_at</c> — added later it would have silently changed the meaning of every list
/// query written in between.
/// </para>
/// <para>
/// <b>No caller scope, unlike the ticket queue.</b> Every asset route is
/// Technician-or-Admin, and an asset has no requester to be narrowed to. If that ever
/// changes, the narrowing goes before <see cref="Filter"/> and not inside it, so no filter
/// can widen it back.
/// </para>
/// <para>
/// <b><see cref="Filter"/> is internal rather than private</b>, following WP-1.12's call for
/// the ticket queue: the moment a dashboard counts assets, it must narrow them with this
/// expression rather than a second one written to match, or a tile saying six opens a list
/// showing five and no test catches it until somebody counts by hand.
/// </para>
/// </remarks>
/// <param name="database">The assets context.</param>
/// <param name="clock">The system clock. The warranty filters are read against it.</param>
internal sealed class ListAssetsHandler(AssetsDbContext database, IClock clock)
{
    /// <summary>Reads a page of the asset list.</summary>
    /// <param name="query">The filters and the ordering.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The page envelope. An empty page is a success, never a 404.</returns>
    public async Task<Result<PagedResult<AssetListItemResponse>>> HandleAsync(
        ListAssetsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = PageRequest.Of(query.Page, query.PageSize);

        // Read once and used for both warranty bounds, so a list cannot be filtered against
        // two different "today"s across a midnight boundary.
        var today = WarrantyWindow.Today(clock.UtcNow);

        var assets = Filter(database.Assets.AsNoTracking(), query, today);

        // The status-code filter is the one that cannot be expressed against the asset table
        // alone: the code lives on the status row. Applied as a subquery rather than by
        // resolving the ids first, so it stays one round trip.
        if (query.StatusCode is { Length: > 0 } codes)
        {
            var wanted = codes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            // Every supplied code was blank. An explicit empty match, rather than silently
            // dropping a filter the caller asked for.
            var matching = wanted.Length == 0
                ? database.AssetStatuses.AsNoTracking().Where(_ => false)
                : database.AssetStatuses.AsNoTracking().Where(status => wanted.Contains(status.Code));

            assets = assets.Where(asset => matching.Any(status => status.Id == asset.AssetStatusId));
        }

        var total = await assets.CountAsync(cancellationToken).ConfigureAwait(false);

        if (total == 0)
        {
            // Nothing matched. Skipping the second round trip is worth the branch on a
            // screen whose empty state is a first-run certainty.
            return PagedResult.Empty<AssetListItemResponse>(page);
        }

        // Joined to the reference data, ordered, paged, and only then projected — in that
        // order, and all in one statement. The shape carrying the join is anonymous rather
        // than a named record because EF sees through an anonymous type in a join and cannot
        // see through a record's positional constructor; ordering by `row.Asset.CreatedAt`
        // over a named record fails to translate outright. It is also why the ordering is
        // applied here rather than after the projection, and the status sort needs the
        // status's SortOrder, which is not a column on the asset at all.
        var rows =
            from asset in assets
            join type in database.AssetTypes.AsNoTracking() on asset.AssetTypeId equals type.Id
            join status in database.AssetStatuses.AsNoTracking() on asset.AssetStatusId equals status.Id
            select new { Asset = asset, Type = type, Status = status };

        var sort = query.Sort ?? AssetSort.AssetTag;

        // A tag and a warranty date are asked for because the front of the list is the
        // wanted end — the label you are looking for, the warranty about to lapse.
        // Everything else means "most recent first".
        var descending = query.Direction switch
        {
            SortDirection.Ascending => false,
            SortDirection.Descending => true,
            _ => sort is not (AssetSort.AssetTag or AssetSort.WarrantyExpiresAt),
        };

        // Every ordering ends at the id. None of the sort columns but the tag is unique —
        // two assets share a status, a warranty date, even a creation instant under a fast
        // enough clock — and a paged list whose order changes between two reads of the same
        // data silently drops and duplicates rows across page boundaries. WP-1.4 learned
        // that from a test rather than from reasoning; the tiebreaker is not optional, and
        // it is applied to the tag too so one rule covers every branch.
        var ordered = sort switch
        {
            AssetSort.CreatedAt => descending
                ? rows.OrderByDescending(r => r.Asset.CreatedAt).ThenByDescending(r => r.Asset.Id)
                : rows.OrderBy(r => r.Asset.CreatedAt).ThenBy(r => r.Asset.Id),

            AssetSort.UpdatedAt => descending
                ? rows.OrderByDescending(r => r.Asset.UpdatedAt).ThenByDescending(r => r.Asset.Id)
                : rows.OrderBy(r => r.Asset.UpdatedAt).ThenBy(r => r.Asset.Id),

            // Nulls land last on the way up and first on the way down, which is PostgreSQL's
            // own default for a plain ORDER BY and is the reading the filter wants: an asset
            // with no recorded warranty is not the most urgent thing on the list.
            AssetSort.WarrantyExpiresAt => descending
                ? rows.OrderByDescending(r => r.Asset.WarrantyExpiresAt).ThenByDescending(r => r.Asset.Id)
                : rows.OrderBy(r => r.Asset.WarrantyExpiresAt).ThenBy(r => r.Asset.Id),

            AssetSort.Status => descending
                ? rows.OrderByDescending(r => r.Status.SortOrder).ThenByDescending(r => r.Asset.Id)
                : rows.OrderBy(r => r.Status.SortOrder).ThenBy(r => r.Asset.Id),

            // Ordered on the normalised tag, so `lap-0042` sorts where `LAP-0042` would
            // rather than after every upper-cased tag in the estate.
            _ => descending
                ? rows.OrderByDescending(r => r.Asset.NormalizedAssetTag).ThenByDescending(r => r.Asset.Id)
                : rows.OrderBy(r => r.Asset.NormalizedAssetTag).ThenBy(r => r.Asset.Id),
        };

        var items = await ordered
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(r => new AssetListItemResponse(
                r.Asset.Id,
                r.Asset.AssetTag,
                r.Asset.Name,
                r.Asset.SerialNumber,
                r.Asset.Manufacturer,
                r.Asset.Model,
                r.Type.Id,
                r.Type.Name,
                r.Status.Id,
                r.Status.Code,
                r.Status.Name,
                r.Asset.AssignedToUserId,
                r.Asset.AssignedToUserName,
                r.Asset.DepartmentId,
                r.Asset.DepartmentName,
                r.Asset.LocationId,
                r.Asset.LocationPath,
                r.Asset.WarrantyExpiresAt,
                r.Asset.CreatedAt,
                r.Asset.UpdatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.From<AssetListItemResponse>(items, total, page);
    }

    /// <summary>Applies the filters that are actually present.</summary>
    /// <remarks>
    /// <para>
    /// Each is skipped when null rather than folded into one expression with null checks
    /// inside it, because a <c>WHERE (@p IS NULL OR col = @p)</c> is the shape that makes
    /// PostgreSQL choose a plan for the parameter it was first given and then keep it for
    /// every other combination.
    /// </para>
    /// <para>
    /// <b>The status-code filter is not here</b>, alone among the filters: it needs the
    /// status table, and this method takes assets. It is applied by the caller, immediately
    /// after. A counter reusing this expression has to do the same.
    /// </para>
    /// </remarks>
    /// <param name="assets">The asset query to narrow.</param>
    /// <param name="query">The filters asked for.</param>
    /// <param name="today">The date the warranty filters are read against.</param>
    /// <returns>The narrowed query.</returns>
    internal static IQueryable<Asset> Filter(IQueryable<Asset> assets, ListAssetsQuery query, DateOnly today)
    {
        if (query.AssetTypeId is { } assetTypeId)
        {
            assets = assets.Where(asset => asset.AssetTypeId == assetTypeId);
        }

        if (query.AssetStatusId is { Length: > 0 } statusIds)
        {
            // Distinct, because a repeated value in the query string would otherwise reach
            // the database as a longer IN list saying the same thing.
            var wanted = statusIds.Distinct().ToArray();

            assets = wanted.Length == 1
                ? assets.Where(asset => asset.AssetStatusId == wanted[0])
                : assets.Where(asset => wanted.Contains(asset.AssetStatusId));
        }

        if (query.DepartmentId is { } departmentId)
        {
            assets = assets.Where(asset => asset.DepartmentId == departmentId);
        }

        if (query.LocationId is { } locationId)
        {
            assets = assets.Where(asset => asset.LocationId == locationId);
        }

        if (query.Unassigned is true)
        {
            // Wins over AssignedToUserId: asking for both is a contradiction, and answering
            // the narrower of the two is the safe reading. The call WP-1.5 made.
            assets = assets.Where(asset => asset.AssignedToUserId == null);
        }
        else if (query.AssignedToUserId is { } holderId)
        {
            assets = assets.Where(asset => asset.AssignedToUserId == holderId);
        }

        assets = FilterByWarranty(assets, query, today);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // The escaping is the shared kernel's (WP-1.12): an unescaped % or _ typed into
            // the box would otherwise become a wildcard over the whole table.
            var pattern = SearchPattern.Containing(query.Search);

            // Hostname is absent because the column is — see ListAssetsQuery.Search. WP-3.1
            // adds it here when it adds it to the model.
            assets = assets.Where(asset =>
                EF.Functions.ILike(asset.AssetTag, pattern, SearchPattern.Escape) ||
                (asset.SerialNumber != null && EF.Functions.ILike(asset.SerialNumber, pattern, SearchPattern.Escape)) ||
                (asset.Name != null && EF.Functions.ILike(asset.Name, pattern, SearchPattern.Escape)) ||
                (asset.Manufacturer != null && EF.Functions.ILike(asset.Manufacturer, pattern, SearchPattern.Escape)) ||
                (asset.Model != null && EF.Functions.ILike(asset.Model, pattern, SearchPattern.Escape)));
        }

        return assets;
    }

    /// <summary>Applies the two warranty filters, which interact.</summary>
    /// <remarks>
    /// <para>
    /// Split out because it is the only pair on this query that does not simply AND. Asking
    /// for both the lapsed warranties and those lapsing within N days is asking for the
    /// union: the two windows are disjoint by construction, so intersecting them could only
    /// ever return nothing, and no caller asks a question whose only possible answer is
    /// empty.
    /// </para>
    /// <para>
    /// <b>The union collapses to one comparison.</b> "Expired" is <c>expiry &lt; today</c>
    /// and "expiring within N" is <c>today &lt;= expiry &lt;= upper</c>; together they are
    /// every recorded expiry at or below <c>upper</c>. Written that way rather than as an
    /// <c>OR</c> of two ranges so the index on <c>warranty_expires_at</c> sees a single
    /// bound.
    /// </para>
    /// <para>
    /// A null <c>warranty_expires_at</c> is excluded from every branch but
    /// <c>warrantyExpired=false</c>, and SQL's three-valued logic does that on its own — a
    /// comparison against NULL is NULL, which is not true. The one branch that must say so
    /// explicitly is the one where "no warranty recorded" is a match, because an asset with
    /// no warranty date has not expired.
    /// </para>
    /// </remarks>
    /// <param name="assets">The asset query to narrow.</param>
    /// <param name="query">The filters asked for.</param>
    /// <param name="today">The date to compare against.</param>
    /// <returns>The narrowed query.</returns>
    private static IQueryable<Asset> FilterByWarranty(
        IQueryable<Asset> assets,
        ListAssetsQuery query,
        DateOnly today)
    {
        if (query.WarrantyExpiringInDays is { } days)
        {
            var upper = WarrantyWindow.UpperBound(today, days);

            return query.WarrantyExpired is true
                ? assets.Where(asset => asset.WarrantyExpiresAt <= upper)
                : ApplyExpired(
                    assets.Where(asset => asset.WarrantyExpiresAt >= today && asset.WarrantyExpiresAt <= upper),
                    query.WarrantyExpired,
                    today);
        }

        return ApplyExpired(assets, query.WarrantyExpired, today);
    }

    /// <summary>Narrows to the lapsed warranties, or to the ones that have not lapsed.</summary>
    /// <param name="assets">The asset query to narrow.</param>
    /// <param name="expired">What was asked for, or <see langword="null"/> for no filter.</param>
    /// <param name="today">The date to compare against.</param>
    /// <returns>The narrowed query.</returns>
    private static IQueryable<Asset> ApplyExpired(IQueryable<Asset> assets, bool? expired, DateOnly today) =>
        expired switch
        {
            true => assets.Where(asset => asset.WarrantyExpiresAt < today),

            // An asset with no warranty date recorded has not expired. Said explicitly,
            // because SQL would otherwise drop it along with the lapsed ones.
            false => assets.Where(asset => asset.WarrantyExpiresAt == null || asset.WarrantyExpiresAt >= today),

            _ => assets,
        };
}
