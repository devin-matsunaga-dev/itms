using Itms.Contracts.Lookups;
using Itms.Platform.Identity;

namespace Itms.Modules.Helpdesk.Features.Tickets;

/// <summary>
/// Who is writing, and the name to cache beside what they wrote.
/// </summary>
/// <remarks>
/// <para>
/// <b>The name comes from <see cref="IUserLookup"/>, not from the cookie's claims.</b> The
/// principal's name claim is the sign-in identifier — "tech" — while every cached display
/// name already on a ticket is the person's actual name, taken from Identity's contract at
/// the moment it was written. A comment thread that alternated between the two would look
/// like two different people. One indexed read is the price, and it is the same read
/// creation and assignment each already make.
/// </para>
/// <para>
/// The claim is nonetheless kept as the fallback for the case where the lookup finds
/// nothing: a signed-in principal whose account has just been removed underneath them is a
/// race, not a reason to lose the comment they typed.
/// </para>
/// </remarks>
/// <param name="Id">The author's user id.</param>
/// <param name="Name">Their display name, to be cached on the row.</param>
internal sealed record TicketActor(Guid Id, string Name)
{
    /// <summary>
    /// Resolves the caller, or <see langword="null"/> when the request carries no
    /// identifiable account.
    /// </summary>
    /// <remarks>
    /// Unreachable behind any of this module's policies — every route requires an
    /// authenticated principal — so a null here means something upstream changed. Callers
    /// treat it as a refusal rather than dereferencing it: the failure direction is closed.
    /// </remarks>
    /// <param name="currentUser">The request's principal.</param>
    /// <param name="users">Identity's public contract.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    public static async Task<TicketActor?> ResolveAsync(
        ICurrentUser currentUser,
        IUserLookup users,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(users);

        if (currentUser.UserId is not { } id)
        {
            return null;
        }

        var account = await users.GetAsync(id, cancellationToken).ConfigureAwait(false);
        var name = account?.DisplayName ?? currentUser.DisplayName;

        return string.IsNullOrWhiteSpace(name) ? null : new TicketActor(id, name);
    }
}
