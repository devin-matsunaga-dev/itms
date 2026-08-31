using System.Globalization;
using Itms.Contracts.Auditing;
using Itms.Contracts.Lookups;
using Itms.Modules.Helpdesk.Auditing;
using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.Tickets;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Identity;
using Itms.Platform.Results;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.TicketComments.AddTicketComment;

/// <summary>
/// Posts a comment, or an internal note, on a ticket.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two questions in order, and the order matters.</b> First, may the caller see this
/// ticket at all — answered by <see cref="TicketScope"/>, and answered with 404 so a
/// requester cannot use the comment route to discover which ticket ids exist. Only then,
/// may they write the <em>kind</em> of comment they asked for — answered by
/// <see cref="TicketVisibility"/>, and answered with 403, because by that point they
/// already hold a ticket they may read and there is nothing left to enumerate.
/// </para>
/// <para>
/// <b>Any status accepts a comment, including Closed.</b> SPEC.md §2 restricts transitions
/// and says nothing about the conversation, and a requester replying to a resolution is how
/// a ticket gets reopened in practice — refusing it would have been a rule this package
/// invented. The thread stays open when the workflow does not.
/// </para>
/// <para>
/// <b>A public comment from anybody but the requester stops the SLA response clock.</b>
/// That is WP-1.8's rule and it is applied here because this is where the fact arrives —
/// see <see cref="Ticket.RecordResponse"/> for why an internal note and a requester's own
/// reply do not count. The stamp is written in the same transaction as the comment, so a
/// post that rolls back cannot leave the ticket claiming somebody answered it.
/// </para>
/// <para>
/// <b>No domain event, deliberately.</b> ARCHITECTURE.md §5 names no comment event and
/// nothing consumes one, so §8's <c>IAuditWriter</c> is what records this — a mutation that
/// does not warrant an event. <b>WP-4.4 is expected to publish <c>TicketCommented</c></b>
/// for SPEC.md §13's in-app notification, and when it does it must decide whether the Audit
/// module's consumer takes over this row (deleting the write below, the way WP-1.6 deleted
/// the status-change one) or whether the event carries notification only. Doing both
/// without choosing is the double-row trap.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system clock. Every instant this writes comes from here.</param>
/// <param name="currentUser">Who is writing.</param>
/// <param name="users">Identity's public contract, for the author's display name.</param>
/// <param name="audit">The audit trail (ARCHITECTURE.md §8).</param>
internal sealed class AddTicketCommentHandler(
    HelpdeskDbContext database,
    IModuleDbSession session,
    IClock clock,
    ICurrentUser currentUser,
    IUserLookup users,
    IAuditWriter audit)
{
    /// <summary>Posts the comment.</summary>
    /// <param name="ticketId">The ticket being commented on.</param>
    /// <param name="request">What is being said, and to whom.</param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>The posted comment, or the failure that stopped it.</returns>
    public async Task<Result<TicketCommentResponse>> HandleAsync(
        Guid ticketId,
        AddTicketCommentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The scoped read, before anything else: a ticket the caller may not see must be
        // indistinguishable from one that does not exist, on this route as on every other.
        var visible = await database.Tickets
            .AsNoTracking()
            .VisibleTo(currentUser)
            .AnyAsync(ticket => ticket.Id == ticketId, cancellationToken)
            .ConfigureAwait(false);

        if (!visible)
        {
            return Result.Failure<TicketCommentResponse>(HelpdeskErrors.TicketNotFound());
        }

        if (request.IsInternal && !TicketVisibility.SeesInternal(currentUser))
        {
            return Result.Failure<TicketCommentResponse>(HelpdeskErrors.InternalCommentForbidden());
        }

        // Outside the transaction, following creation and assignment: it is a read of
        // another module's data through its contract, and holding a lock across it would
        // serialise the thread behind Identity.
        var author = await TicketActor.ResolveAsync(currentUser, users, cancellationToken).ConfigureAwait(false);

        if (author is null)
        {
            return Result.Failure<TicketCommentResponse>(HelpdeskErrors.TicketNotFound());
        }

        var comment = TicketComment.Post(
            ticketId,
            request.Body,
            request.IsInternal,
            author.Id,
            author.Name,
            clock.UtcNow);

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token).ConfigureAwait(false);

                database.TicketComments.Add(comment);

                await StopResponseClockAsync(comment, token).ConfigureAwait(false);

                await database.SaveChangesAsync(token).ConfigureAwait(false);

                // Inside the transaction, so a post that rolls back leaves no entry saying
                // it happened. Keyed on the ticket rather than the comment, so one query
                // returns everything that ever happened to TKT-0042.
                //
                // The body is not in the diff. §8 wants changed fields, a new comment has no
                // before, and copying eight thousand characters into an append-only table
                // that can never be corrected buys nothing the comment row does not already
                // hold. What the trail needs is that somebody said something, when, and
                // whether the requester could see it.
                await audit.WriteAsync(
                    new AuditEntry(
                        HelpdeskAudit.TicketCommented,
                        HelpdeskAudit.TicketEntityType,
                        ticketId.ToString(),
                        HelpdeskAudit.Changes()
                            .Set("commentId", comment.Id.ToString())
                            .Set("isInternal", comment.IsInternal.ToString(CultureInfo.InvariantCulture))
                            .Set("length", comment.Body.Length.ToString(CultureInfo.InvariantCulture))),
                    token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success(new TicketCommentResponse(
            comment.Id,
            comment.TicketId,
            comment.Body,
            comment.IsInternal,
            comment.AuthorId,
            comment.AuthorName,
            comment.CreatedAt));
    }

    /// <summary>
    /// Records this comment as the ticket's first response, if that is what it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The ticket is loaded only when the comment could possibly count</b> — a public
    /// one — and it is written only when it is the first. An internal note costs nothing,
    /// and neither does the second reply on a busy thread: EF issues no <c>UPDATE</c> for
    /// an entity nothing changed on.
    /// </para>
    /// <para>
    /// <b>Tracked, which brings the <c>xmin</c> token with it.</b> A ticket moved by
    /// somebody else between this read and the save makes the whole post fail with the
    /// module's concurrency error rather than silently losing the stamp. The window is one
    /// comment wide — the first public reply, and no other — which is why that trade is
    /// worth making rather than reaching for a blind update that would write the column
    /// from outside the entity.
    /// </para>
    /// </remarks>
    /// <param name="comment">The comment being posted.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    private async Task StopResponseClockAsync(TicketComment comment, CancellationToken cancellationToken)
    {
        if (comment.IsInternal)
        {
            return;
        }

        var ticket = await database.Tickets
            .FirstOrDefaultAsync(candidate => candidate.Id == comment.TicketId, cancellationToken)
            .ConfigureAwait(false);

        // Null only if the ticket was deleted between the visibility check and here; the
        // comment insert would then fail on its own foreign key, which is the right failure.
        if (ticket is null || ticket.RequesterId == comment.AuthorId)
        {
            return;
        }

        ticket.RecordResponse(comment.CreatedAt);
    }
}
