using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Itms.Platform.Identity;

/// <summary>
/// The request-scoped <see cref="ICurrentUser"/>, reading the principal ASP.NET has
/// already established. Everything it exposes is <see langword="null"/> or empty
/// outside a request, which is what a background dispatcher or a hosted service sees.
/// </summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    /// <inheritdoc />
    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <inheritdoc />
    public string? DisplayName =>
        Principal?.FindFirstValue(ClaimTypes.Name) ?? Principal?.Identity?.Name;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];

    /// <inheritdoc />
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    /// <inheritdoc />
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
}
