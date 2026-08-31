using Itms.Modules.Helpdesk.Domain;
using Itms.Platform.Identity;

namespace Itms.Modules.Helpdesk.Features.Tickets;

/// <summary>
/// The row-level rule from ARCHITECTURE.md §7, written once: a <b>User</b> may read the
/// tickets they raised, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a query filter and not an endpoint check.</b> §7 says the React app
/// hiding what a role cannot use "is never the enforcement". A filter composed into the
/// query cannot be forgotten by a caller who remembers to load a ticket but forgets to
/// ask whether they may: there is no ticket to forget about, because the query never
/// returned one. Every read of the ticket table outside the create path goes through
/// <see cref="VisibleTo"/> — the list, the detail, and the timeline alike.
/// </para>
/// <para>
/// <b>A ticket somebody else raised answers 404, not 403.</b> ARCHITECTURE.md §6 forbids a
/// 404 disguising a 403 "except where enumerating IDs would leak", and this is exactly
/// that exception: a 403 on a ticket id and a 404 on a nonexistent one would let anybody
/// with an account walk the id space and count the tickets they cannot see. Refusing to
/// distinguish them is the point.
/// </para>
/// <para>
/// <b>WP-1.7 inherits this and must extend it.</b> The scope answers <em>which tickets</em>
/// a User may read. It says nothing about <em>which parts</em> of one, and internal notes
/// are precisely a part they may not see. When comments arrive, filtering the ticket is no
/// longer sufficient — the projection has to filter too, and WP-1.7's own criterion is
/// that a User fetching their own ticket receives no internal notes in the payload.
/// Nothing here will catch that omission.
/// </para>
/// </remarks>
internal static class TicketScope
{
    /// <summary>
    /// Narrows <paramref name="tickets"/> to what <paramref name="user"/> is allowed to
    /// see.
    /// </summary>
    /// <remarks>
    /// A Technician or an Admin sees the whole queue — that is what SPEC.md §14 gives
    /// them. Anybody else sees only what they raised. An anonymous caller cannot reach
    /// this (every route is behind a policy), but if one ever did, the null user id
    /// matches no row: the failure direction is empty, never everything.
    /// </remarks>
    /// <param name="tickets">The ticket query being built.</param>
    /// <param name="user">Who is asking.</param>
    /// <returns>The query, narrowed if it needs to be.</returns>
    public static IQueryable<Ticket> VisibleTo(this IQueryable<Ticket> tickets, ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        ArgumentNullException.ThrowIfNull(user);

        if (SeesEveryTicket(user))
        {
            return tickets;
        }

        var requesterId = user.UserId;

        return tickets.Where(ticket => ticket.RequesterId == requesterId);
    }

    /// <summary>
    /// True when <paramref name="user"/> holds the queue-wide roles, so the scope is not
    /// narrowed.
    /// </summary>
    /// <remarks>
    /// Also what the create handler asks before letting a caller name somebody else as the
    /// requester.
    /// </remarks>
    /// <param name="user">Who is asking.</param>
    public static bool SeesEveryTicket(ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.IsInRole(ItmsRoles.Technician) || user.IsInRole(ItmsRoles.Admin);
    }
}
