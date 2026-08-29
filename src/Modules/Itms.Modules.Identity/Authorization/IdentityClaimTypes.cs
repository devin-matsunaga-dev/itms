namespace Itms.Modules.Identity.Authorization;

/// <summary>The claims this system adds on top of the ones ASP.NET Core Identity issues.</summary>
public static class IdentityClaimTypes
{
    /// <summary>
    /// The id of the <c>identity.sessions</c> row this cookie was issued against. It is
    /// what turns a signed cookie into a revocable session: the cookie is valid only
    /// while the row it names is.
    /// </summary>
    public const string SessionId = "itms:sid";
}
