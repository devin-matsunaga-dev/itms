namespace Itms.Modules.Identity.Auditing;

/// <summary>
/// The action identifiers this module writes through <c>IAuditWriter</c>.
/// </summary>
/// <remarks>
/// <para>
/// A sign-in changes no row, so it produces no domain event and would otherwise leave
/// no trace — which is exactly the case ARCHITECTURE.md §8 keeps <c>IAuditWriter</c>
/// for. They are declared here rather than shared with the Audit module because a
/// module may not reference another module (§3 rule 2).
/// </para>
/// <para>
/// They are stable strings stored in a text column. Renaming one orphans the history it
/// describes, so add rather than rename.
/// </para>
/// </remarks>
internal static class IdentityAuditActions
{
    /// <summary>Credentials were accepted and a session was opened.</summary>
    public const string LoginSucceeded = "auth.login_succeeded";

    /// <summary>Credentials were refused. The reason is recorded on the entry.</summary>
    public const string LoginFailed = "auth.login_failed";

    /// <summary>The entity type both of them are about.</summary>
    public const string UserEntityType = "User";
}
