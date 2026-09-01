namespace Itms.Contracts.Lookups;

/// <summary>
/// The fields another module is allowed to know about a user. It carries no
/// credential state of any kind — that never leaves Identity.
/// </summary>
/// <param name="Id">The user's id.</param>
/// <param name="DisplayName">The name to show on a ticket, comment, or asset history row.</param>
/// <param name="Email">The address notifications go to.</param>
/// <param name="DepartmentId">Their department, if set.</param>
/// <param name="LocationId">Their location, if set.</param>
/// <param name="IsActive">False once deactivated. A deactivated user still owns their history (invariant 9).</param>
/// <param name="Roles">
/// The roles the account holds, from the three <c>ItmsRoles</c> names. Carried because
/// eligibility rules are a module's own business but the role that decides them is
/// Identity's: Helpdesk may only assign a ticket to a technician (WP-1.6), and without
/// this it would have to either reference <c>Modules.Identity</c> or trust the caller.
/// A membership list rather than a workflow flag, so no one module's question is baked
/// into the contract.
/// </param>
public sealed record UserSummary(
    Guid Id,
    string DisplayName,
    string Email,
    Guid? DepartmentId,
    Guid? LocationId,
    bool IsActive,
    IReadOnlyList<string> Roles);

/// <summary>
/// How every other module reads users. Helpdesk needs a requester's name, Assets
/// needs a holder's name, Notifications needs an address — none of them references
/// <c>Modules.Identity</c>.
/// </summary>
public interface IUserLookup
{
    /// <summary>The user with <paramref name="userId"/>, or <see langword="null"/> if no such user exists.</summary>
    Task<UserSummary?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The users in <paramref name="userIds"/> that exist, batched for list screens.</summary>
    Task<IReadOnlyList<UserSummary>> GetManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    /// <summary>Active users whose name or email matches <paramref name="term"/>, for requester and assignee pickers.</summary>
    /// <remarks>
    /// A blank <paramref name="term"/> means "no filter" and lists the first
    /// <paramref name="limit"/> active users by name — it does <b>not</b> mean "match
    /// nothing". A picker opens by asking with no term, so an implementation that returned
    /// an empty list for a blank one would leave every picker in the product permanently
    /// empty. That is not hypothetical: it shipped that way from WP-0.5 to WP-1.14 and made
    /// ticket assignment impossible through the interface.
    /// </remarks>
    /// <param name="term">A free-text fragment, or blank for no filter.</param>
    /// <param name="limit">The most results to return; pickers want a short list, not a page.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<UserSummary>> SearchAsync(string term, int limit, CancellationToken cancellationToken);
}
