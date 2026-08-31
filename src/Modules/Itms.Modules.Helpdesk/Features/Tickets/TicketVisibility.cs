using Itms.Modules.Helpdesk.Domain;
using Itms.Platform.Identity;

namespace Itms.Modules.Helpdesk.Features.Tickets;

/// <summary>
/// The rule from SPEC.md §14 that a <b>User</b> gets "no internal notes", written once: an
/// internal comment and an internal attachment are invisible to anybody outside the queue,
/// including the person whose ticket it is.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the other half of <see cref="TicketScope"/>.</b> The scope answers
/// <em>which tickets</em> a caller may read, and its own remarks said WP-1.7 would have to
/// answer <em>which parts of one</em> — because a requester can legitimately read their own
/// ticket, and the note a technician wrote on it is the one thing on that ticket they may
/// not see. Composing the scope alone is no longer sufficient anywhere a comment or an
/// attachment is projected.
/// </para>
/// <para>
/// <b>Why it is a query filter, for the same reason the scope is.</b> ARCHITECTURE.md §7
/// says the React app hiding what a role cannot use "is never the enforcement", and a
/// filter that runs in the database cannot be forgotten by a caller who remembers to load
/// the thread but forgets to ask whether every line in it is theirs to see. There is no
/// note to leak, because the query never returned one. The four reads that touch these two
/// tables — the comment list, the attachment list, the detail's embedded heads, and the
/// download — all compose it, and the download composes it before it so much as learns the
/// file's name on disk.
/// </para>
/// <para>
/// <b>An internal row is absent, not redacted.</b> Nothing in the payload says a note was
/// withheld: a count, a placeholder, or a gap in a sequence would each tell a requester
/// that their technician wrote something about them, which is exactly the fact an internal
/// note exists to keep. That is also why <c>hasMoreComments</c> is computed after the
/// filter rather than before it.
/// </para>
/// </remarks>
internal static class TicketVisibility
{
    /// <summary>
    /// True when <paramref name="user"/> may read internal notes and internal attachments,
    /// and therefore also create them.
    /// </summary>
    /// <remarks>
    /// Deliberately the same predicate as <see cref="TicketScope.SeesEveryTicket"/> rather
    /// than a second one that happens to agree with it today. "Sees the whole queue" and
    /// "is inside the queue's private conversation" are the same population — Technician
    /// and Admin, per SPEC.md §14 — and two predicates that must never disagree are better
    /// written as one.
    /// </remarks>
    /// <param name="user">Who is asking.</param>
    public static bool SeesInternal(ICurrentUser user) => TicketScope.SeesEveryTicket(user);

    /// <summary>
    /// Narrows <paramref name="comments"/> to the lines <paramref name="user"/> is allowed
    /// to read.
    /// </summary>
    /// <remarks>
    /// The failure direction is closed: anybody who is not demonstrably staff loses the
    /// internal lines, including an anonymous caller who could not reach here anyway.
    /// </remarks>
    /// <param name="comments">The comment query being built.</param>
    /// <param name="user">Who is asking.</param>
    /// <returns>The query, narrowed if it needs to be.</returns>
    public static IQueryable<TicketComment> VisibleTo(this IQueryable<TicketComment> comments, ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(comments);
        ArgumentNullException.ThrowIfNull(user);

        return SeesInternal(user) ? comments : comments.Where(comment => !comment.IsInternal);
    }

    /// <summary>
    /// Narrows <paramref name="attachments"/> to the files <paramref name="user"/> is
    /// allowed to see and fetch.
    /// </summary>
    /// <param name="attachments">The attachment query being built.</param>
    /// <param name="user">Who is asking.</param>
    /// <returns>The query, narrowed if it needs to be.</returns>
    public static IQueryable<TicketAttachment> VisibleTo(
        this IQueryable<TicketAttachment> attachments,
        ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(user);

        return SeesInternal(user) ? attachments : attachments.Where(attachment => !attachment.IsInternal);
    }
}
