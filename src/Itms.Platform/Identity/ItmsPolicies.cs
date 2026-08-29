namespace Itms.Platform.Identity;

/// <summary>
/// The names of the authorization policies every endpoint in the system is guarded by.
/// </summary>
/// <remarks>
/// The names live in the shared kernel and the policies themselves are defined by the
/// Identity module, because ARCHITECTURE.md §7 makes authorization policy-based and
/// evaluated server-side on <em>every</em> endpoint — which means every module has to be
/// able to name a policy without referencing <c>Modules.Identity</c>. Spelling a policy
/// as a string literal in an endpoint is how a typo becomes an unguarded route.
/// </remarks>
public static class ItmsPolicies
{
    /// <summary>Admin only: configuration, user and role management, the audit log.</summary>
    public const string Admin = "policy.admin";

    /// <summary>Technician or Admin: the whole operational surface — tickets, assets, monitoring, alerts.</summary>
    public const string Technician = "policy.technician";

    /// <summary>Any authenticated, active account, including an end user acting on their own tickets.</summary>
    public const string Authenticated = "policy.authenticated";
}
