using Itms.Platform.Paging;
using Microsoft.AspNetCore.Mvc;

namespace Itms.Modules.Identity.Features.Users.ListUsers;

/// <summary>
/// Everything the user directory can be narrowed and ordered by, bound from the query
/// string.
/// </summary>
/// <remarks>
/// <para>
/// One type rather than half a dozen parameters on the endpoint delegate, so the filter set
/// is a thing with a name that OpenAPI describes and the generated client can hold — the
/// shape <c>ListAssetsQuery</c> established at WP-2.3.
/// </para>
/// <para>
/// <b>This is the query WP-2.7 was granted a server change for.</b> Until then
/// <c>GET /api/v1/users</c> was the picker search WP-0.5 wrote: active accounts only,
/// <c>?search=&amp;limit=</c>, capped at fifty, answering a bare array with no total. A
/// picker is happy with that; a directory screen is not, because CONVENTIONS.md requires
/// every list screen to keep its filters, its ordering and its page in the URL, and none of
/// those three can be honoured against a response that does not say how many rows exist.
/// The picker did not need a second route — it needs a page of one, which is what
/// <see cref="PageSize"/> gives it.
/// </para>
/// <para>
/// <b>The old <c>?limit=</c> is gone rather than kept as an alias.</b> Two ways to say how many
/// rows a caller wants is two things to keep agreeing about a clamp, and this repository has
/// settled on one addressing scheme per question twice already (WP-1.16, WP-2.6a). The two
/// picker call sites moved to <c>?pageSize=200</c> in the same change, and the API-wide
/// maximum of two hundred is now what bounds them instead of the lookup's own fifty.
/// </para>
/// </remarks>
public sealed class ListUsersQuery
{
    /// <summary>
    /// Free text matched against the display name and the email address,
    /// case-insensitively.
    /// </summary>
    /// <remarks>
    /// The same two columns <c>IUserLookup.SearchAsync</c> matches, deliberately: this route
    /// is still the product's people-picker, and a directory that found somebody a picker
    /// could not would be two search rules wearing one route. The sign-in name is not
    /// searched because it does not leave Identity — <c>UserSummary</c> carries no
    /// <c>userName</c>, and matching on a field the caller can never see produces results
    /// that look arbitrary from outside.
    /// </remarks>
    [FromQuery(Name = "search")]
    public string? Search { get; init; }

    /// <summary>Only people recorded as sitting in this department.</summary>
    [FromQuery(Name = "departmentId")]
    public Guid? DepartmentId { get; init; }

    /// <summary>Only people recorded as sitting at this location.</summary>
    /// <remarks>
    /// Matches the location exactly and does not descend the tree, exactly as the asset
    /// register's does: the subtree is answerable only from Directory's materialised path,
    /// and reasoning about that format here is what §3 rule 6 keeps this module out of.
    /// </remarks>
    [FromQuery(Name = "locationId")]
    public Guid? LocationId { get; init; }

    /// <summary>Only people holding this role — <c>Admin</c>, <c>Technician</c>, or <c>User</c>.</summary>
    /// <remarks>
    /// Matched on the role's name, case-insensitively. An unrecognised role is not an error;
    /// it is a filter matching nothing, which is what it says — the reading WP-2.3 settled
    /// for an unrecognised asset status code, and the one that keeps a hand-written URL from
    /// being a 400 rather than an empty list.
    /// </remarks>
    [FromQuery(Name = "role")]
    public string? Role { get; init; }

    /// <summary>
    /// <see langword="true"/> to include deactivated accounts. Defaults to false — active
    /// people only.
    /// </summary>
    /// <remarks>
    /// <b>The default is what keeps every existing picker correct.</b> <c>SearchAsync</c> has
    /// filtered to active accounts since WP-0.5, because issuing equipment to somebody who
    /// can no longer sign in is not a thing anybody means to do. A directory has the opposite
    /// need at least sometimes — invariant 9 keeps a deactivated person's tickets, comments
    /// and asset history forever, and the screen that explains where a laptop went has to be
    /// able to name its holder. So the deactivated are reachable, and never by accident.
    /// </remarks>
    [FromQuery(Name = "includeInactive")]
    public bool? IncludeInactive { get; init; }

    /// <summary>What to order by. Defaults to <see cref="UserSort.DisplayName"/>.</summary>
    [FromQuery(Name = "sort")]
    public UserSort? Sort { get; init; }

    /// <summary>
    /// Which way to order. Defaults to <see cref="SortDirection.Ascending"/> for the name and
    /// the address, where the useful end is the front, and to
    /// <see cref="SortDirection.Descending"/> for the creation date.
    /// </summary>
    [FromQuery(Name = "direction")]
    public SortDirection? Direction { get; init; }

    /// <summary>The 1-based page number. Out-of-range values are clamped, not rejected.</summary>
    [FromQuery(Name = "page")]
    public int? Page { get; init; }

    /// <summary>How many people to a page, up to the API-wide maximum of 200.</summary>
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; }
}
