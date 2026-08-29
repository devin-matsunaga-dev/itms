using System.Security.Claims;
using Itms.Modules.Identity.Domain;
using Itms.Modules.Identity.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Itms.Modules.Identity.Authorization;

/// <summary>
/// Builds the principal the cookie carries.
/// </summary>
/// <remarks>
/// <c>Itms.Platform</c>'s <c>ICurrentUser</c> reads the actor's display name from
/// <see cref="ClaimTypes.Name"/>, and that is the name every ticket, comment, and audit
/// row will show. Identity would otherwise put the sign-in name there, so the sign-in
/// name is moved to <see cref="ClaimTypes.Upn"/> (in <c>IdentityOptions</c>) and the
/// display name takes its place. Doing it here rather than in the shared kernel keeps
/// the kernel free of any knowledge of how a principal is minted.
/// </remarks>
/// <param name="userManager">The user store.</param>
/// <param name="roleManager">The role store, for role claims.</param>
/// <param name="options">Identity options, including the claim type mapping.</param>
public sealed class ItmsUserClaimsPrincipalFactory(
    UserManager<ItmsUser> userManager,
    RoleManager<ItmsRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ItmsUser, ItmsRole>(userManager, roleManager, options)
{
    /// <inheritdoc />
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ItmsUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var identity = await base.GenerateClaimsAsync(user).ConfigureAwait(false);
        identity.AddClaim(new Claim(ClaimTypes.Name, user.DisplayName));
        return identity;
    }
}
