namespace Itms.Platform.Identity;

/// <summary>
/// Who is making the current request. Handlers, the audit writer, and the
/// <c>created_by</c>/<c>updated_by</c> columns all read the actor from here rather
/// than from <c>HttpContext</c>, so none of them has to reference ASP.NET and all of
/// them can be tested with a stub.
/// </summary>
/// <remarks>
/// This reads whatever principal the request already carries. It configures no
/// authentication and issues no claims — WP-0.5 owns that.
/// </remarks>
public interface ICurrentUser
{
    /// <summary>The authenticated user's id, or <see langword="null"/> when the request is anonymous.</summary>
    Guid? UserId { get; }

    /// <summary>The authenticated user's display name, or <see langword="null"/> when the request is anonymous.</summary>
    string? DisplayName { get; }

    /// <summary>The roles on the current principal. Empty when anonymous.</summary>
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>True when a principal is authenticated.</summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// True when the current principal holds <paramref name="role"/>. This is a
    /// convenience for handlers; endpoint authorization is still policy-based and
    /// evaluated by ASP.NET (ARCHITECTURE.md §7).
    /// </summary>
    /// <param name="role">One of the constants on <see cref="ItmsRoles"/>.</param>
    bool IsInRole(string role);

    /// <summary>
    /// The caller's IP address as seen by the host, or <see langword="null"/> when there
    /// is no request. Audit entries are required to record it (ARCHITECTURE.md §8).
    /// </summary>
    string? IpAddress { get; }
}
