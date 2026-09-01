using Itms.Contracts.Auditing;
using Itms.Contracts.Lookups;
using Itms.Modules.Helpdesk.Auditing;
using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.TicketHistory;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Modules.Helpdesk.Persistence.Configurations;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.Tickets.LinkTicketAsset;

/// <summary>
/// Names the asset a ticket is about, changes it, or clears it — the join SPEC.md §4 is
/// built on.
/// </summary>
/// <remarks>
/// <para>
/// <b>The handler decides nothing about whether the change is allowed.</b>
/// <see cref="Ticket.LinkAsset"/> does, the same division WP-1.3 drew for the status change
/// and WP-1.6 for the assignment. What lives here is the one question the entity cannot
/// answer: whether the asset exists at all, which is a fact about Assets' rows and is read
/// through <see cref="IAssetLookup"/> — this is that contract's first consumer.
/// </para>
/// <para>
/// <b>No cached tag is written to the ticket.</b> The link is an id and nothing else
/// (§3 rule 6); the tag on this response and on the detail read is resolved live. That is
/// WP-2.5's decision at the human's direction, and it is why a renamed asset does not go
/// stale on tickets already filed the way a renamed department does.
/// </para>
/// <para>
/// <b>No domain event, and an <see cref="IAuditWriter"/> call instead.</b>
/// ARCHITECTURE.md §5 lists eleven events and a ticket-asset link is not among them;
/// adding a twelfth is an architecture change rather than a package's. §8 keeps
/// <c>IAuditWriter</c> for exactly the mutations that do not warrant an event, and a
/// ticket modification is mandatory coverage under SPEC.md §15 — the same call WP-1.7 made
/// for comments and attachments. Nothing here re-audits an action the Audit module already
/// derives from an event, which is the trap <c>HelpdeskAudit</c> warns about.
/// </para>
/// <para>
/// The ticket, its history entry, and the audit row commit in one transaction, so a link
/// that is rolled back leaves nothing claiming it happened.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock. Every instant this writes comes from here.</param>
/// <param name="currentUser">Who is making the request.</param>
/// <param name="assets">Assets' public contract, for the asset's existence and its display text.</param>
/// <param name="history">The ticket's own timeline (invariant 3).</param>
/// <param name="audit">The append-only trail, enrolled in this handler's own transaction.</param>
internal sealed class LinkTicketAssetHandler(
    HelpdeskDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IAssetLookup assets,
    TicketHistoryRecorder history,
    IAuditWriter audit)
{
    /// <summary>Applies <paramref name="request"/> to the ticket.</summary>
    /// <param name="ticketId">The ticket whose related asset is changing.</param>
    /// <param name="request">The asset to name, or null to clear the link.</param>
    /// <param name="expectedVersions">
    /// The row versions the caller's <c>If-Match</c> will accept, or <see langword="null"/>
    /// when it stated no precondition.
    /// </param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>The link that happened, or the failure that stopped it.</returns>
    public async Task<Result<TicketAssetLink>> HandleAsync(
        Guid ticketId,
        LinkTicketAssetRequest request,
        IReadOnlySet<uint>? expectedVersions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Read before the transaction, following WP-1.5 and WP-1.6: this is a cross-module
        // read, and holding a row lock on the ticket across it would serialise linking
        // behind Assets.
        AssetSummary? asset = null;

        if (request.AssetId is { } assetId)
        {
            asset = await assets.GetAsync(assetId, cancellationToken).ConfigureAwait(false);

            if (asset is null)
            {
                return Result.Failure<TicketAssetLink>(HelpdeskErrors.RelatedAssetNotFound());
            }
        }

        Error? failure = null;
        TicketAssetLink? link = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                // Tracked, not AsNoTracking: this is a write, and the xmin token WP-1.2
                // mapped only does its job on a tracked entity. Unscoped, like the sibling
                // writes: the route is Technician-or-Admin, and TicketScope narrows nobody
                // who can reach it.
                var ticket = await database.Tickets
                    .FirstOrDefaultAsync(candidate => candidate.Id == ticketId, token)
                    .ConfigureAwait(false);

                if (ticket is null)
                {
                    failure = HelpdeskErrors.TicketNotFound();
                    return;
                }

                var entry = database.Entry(ticket);

                // The caller's precondition, checked before anything is attempted — the
                // whole point of the 412.
                if (expectedVersions is not null
                    && !expectedVersions.Contains(entry.Property<uint>(TicketConfiguration.VersionProperty).CurrentValue))
                {
                    failure = HelpdeskErrors.TicketPreconditionFailed();
                    return;
                }

                var before = TicketSnapshot.Of(ticket);
                var now = clock.UtcNow;

                var moved = ticket.LinkAsset(request.AssetId, now, currentUser.UserId);

                if (moved.IsFailure)
                {
                    failure = moved.Error;
                    return;
                }

                // The previous asset's display text, resolved only when there was one — so
                // the common case, linking a ticket that named nothing, costs no extra read
                // at all. The recorder resolves its own pair for the timeline; keeping the
                // two separate is what stops the response's shape from dictating the
                // history's.
                var previous = before.RelatedAssetId is { } previousId
                    ? await assets.GetAsync(previousId, token).ConfigureAwait(false)
                    : null;

                // Added, not saved: the entry reaches the database on the SaveChanges
                // below, inside this transaction. That is invariant 3.
                await history.RecordAsync(ticket, before, now, token).ConfigureAwait(false);

                await audit.WriteAsync(
                    new AuditEntry(
                        HelpdeskAudit.TicketAssetLinked,
                        HelpdeskAudit.TicketEntityType,
                        ticket.Id.ToString(),
                        HelpdeskAudit.Changes()
                            .Moved("relatedAssetId", before.RelatedAssetId?.ToString(), ticket.RelatedAssetId?.ToString())
                            .Moved("relatedAssetTag", previous?.AssetTag, asset?.AssetTag)),
                    token).ConfigureAwait(false);

                try
                {
                    await database.SaveChangesAsync(token).ConfigureAwait(false);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Somebody moved the ticket between the read and the write. A 409 the
                    // client can retry, not the 500 an unhandled one would be.
                    failure = HelpdeskErrors.TicketChangedConcurrently();
                    return;
                }

                link = new TicketAssetLink(
                    new TicketAssetLinkResponse(
                        ticket.Id,
                        ticket.Number,
                        TicketRelatedAssetResponse.From(previous),
                        TicketRelatedAssetResponse.From(asset),
                        now),
                    // Read back off the tracked entry rather than from before the write:
                    // xmin is ValueGeneratedOnAddOrUpdate, so EF returns the new value with
                    // the UPDATE and refreshes it here.
                    entry.Property<uint>(TicketConfiguration.VersionProperty).CurrentValue);
            },
            cancellationToken).ConfigureAwait(false);

        return failure is null
            ? Result.Success(link!)
            : Result.Failure<TicketAssetLink>(failure);
    }
}
