namespace Itms.Contracts.Events;

/// <summary>
/// A user was deactivated. Consumers revoke sessions, reassign or flag open work,
/// and refresh cached display names. They do not delete anything: invariant 9 says
/// deactivation never removes a user's tickets, comments, or asset history.
/// </summary>
/// <param name="UserId">The deactivated user.</param>
/// <param name="DisplayName">Their display name at deactivation, so cached copies can be refreshed rather than looked up.</param>
public sealed record UserDeactivated(Guid UserId, string DisplayName) : DomainEvent;
