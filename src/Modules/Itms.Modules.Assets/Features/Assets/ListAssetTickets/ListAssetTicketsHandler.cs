using Itms.Contracts.Lookups;
using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Paging;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.Assets.ListAssetTickets;

/// <summary>
/// Reads the support history of one asset — every ticket that names it, newest first.
/// </summary>
/// <remarks>
/// <para>
/// <b>Assets does not query the helpdesk schema, and could not.</b> §3 rule 1 gives each
/// module its own tables and rule 2 routes the read through the owning module's contract,
/// so this asks <see cref="ITicketLookup"/> and the architecture test keeps it honest:
/// <c>Itms.Modules.Assets</c> references <c>Itms.Contracts</c> and no module assembly at
/// all.
/// </para>
/// <para>
/// <b>The asset is checked first, and against this module's own table.</b> An asset that
/// does not exist answers 404, while one that exists and has never had a ticket answers an
/// empty page — asking the lookup alone could not tell those apart, and an empty history
/// for an id nobody has is the more misleading of the two. It is the same shape
/// <c>ListAssetHistoryHandler</c> uses, and the soft-delete filter applies, so a deleted
/// asset is a 404 here too.
/// </para>
/// <para>
/// <b>Every linked ticket, whatever its status.</b> An asset's history is the whole story
/// of that machine, and splitting it into open and past is a user page's question rather
/// than a machine's (WP-2.5, at the human's direction). Nothing is scoped by requester
/// either: the route is Technician-or-Admin, so no caller who can reach it would be
/// narrowed.
/// </para>
/// </remarks>
/// <param name="database">The assets context, for the existence check.</param>
/// <param name="tickets">Helpdesk's public contract.</param>
internal sealed class ListAssetTicketsHandler(AssetsDbContext database, ITicketLookup tickets)
{
    /// <summary>Reads a page of the asset's tickets.</summary>
    /// <param name="assetId">The asset whose tickets are wanted.</param>
    /// <param name="page">The page to read.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The page envelope, or a not-found failure when there is no such asset.</returns>
    public async Task<Result<PagedResult<TicketSummary>>> HandleAsync(
        Guid assetId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var exists = await database.Assets
            .AsNoTracking()
            .AnyAsync(asset => asset.Id == assetId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            return Result.Failure<PagedResult<TicketSummary>>(AssetsErrors.AssetNotFound());
        }

        var result = await tickets
            .GetForAssetAsync(assetId, page.Page, page.PageSize, cancellationToken)
            .ConfigureAwait(false);

        // The contract carries its own page shape, because Itms.Contracts may reference
        // nothing in the solution and PagedResult lives in the shared kernel. Mapping it
        // onto the API envelope is this one line, which is the price of that rule.
        return Result.Success(
            new PagedResult<TicketSummary>(result.Items, result.Total, result.Page, result.PageSize));
    }
}
