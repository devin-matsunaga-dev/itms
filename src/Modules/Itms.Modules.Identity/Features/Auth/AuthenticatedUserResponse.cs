namespace Itms.Modules.Identity.Features.Auth;

/// <summary>
/// The account the caller is signed in as. Returned by both <c>/login</c> and
/// <c>/me</c>, so the client has one shape to hold for "who am I" however it got there.
/// </summary>
/// <param name="Id">The user id.</param>
/// <param name="UserName">The sign-in name.</param>
/// <param name="Email">Their address.</param>
/// <param name="DisplayName">The name shown throughout the product.</param>
/// <param name="Roles">Their roles. The client uses these to hide what a role cannot use; hiding is never the enforcement (ARCHITECTURE.md §7).</param>
/// <param name="DepartmentId">Their department, if set.</param>
/// <param name="LocationId">Their location, if set.</param>
public sealed record AuthenticatedUserResponse(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    Guid? DepartmentId,
    Guid? LocationId);
