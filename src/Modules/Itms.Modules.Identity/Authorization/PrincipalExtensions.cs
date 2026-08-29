using System.Security.Claims;

namespace Itms.Modules.Identity.Authorization;

/// <summary>Reads the claims this module adds to a principal.</summary>
public static class PrincipalExtensions
{
    /// <summary>
    /// The session the caller's cookie was issued against.
    /// </summary>
    /// <param name="principal">The caller's principal.</param>
    /// <param name="sessionId">The session id, when the claim is present and well formed.</param>
    /// <returns><see langword="true"/> when a session id was found.</returns>
    public static bool TryGetSessionId(this ClaimsPrincipal principal, out Guid sessionId)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return Guid.TryParse(principal.FindFirstValue(IdentityClaimTypes.SessionId), out sessionId);
    }
}
