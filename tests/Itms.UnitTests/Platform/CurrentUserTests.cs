using System.Security.Claims;
using Itms.Platform.Identity;
using Microsoft.AspNetCore.Http;

namespace Itms.UnitTests.Platform;

public sealed class CurrentUserTests
{
    private static readonly Guid UserId = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");

    [Fact]
    public void An_authenticated_principal_is_projected_onto_the_accessor()
    {
        var currentUser = For(Authenticated(UserId, "Dana Okafor", ItmsRoles.Technician));

        currentUser.IsAuthenticated.ShouldBeTrue();
        currentUser.UserId.ShouldBe(UserId);
        currentUser.DisplayName.ShouldBe("Dana Okafor");
        currentUser.Roles.ShouldBe([ItmsRoles.Technician]);
        currentUser.IsInRole(ItmsRoles.Technician).ShouldBeTrue();
        currentUser.IsInRole(ItmsRoles.Admin).ShouldBeFalse();
    }

    [Fact]
    public void An_anonymous_request_has_no_user_and_no_roles()
    {
        var currentUser = For(new ClaimsPrincipal(new ClaimsIdentity()));

        currentUser.IsAuthenticated.ShouldBeFalse();
        currentUser.UserId.ShouldBeNull();
        currentUser.DisplayName.ShouldBeNull();
        currentUser.Roles.ShouldBeEmpty();
        currentUser.IsInRole(ItmsRoles.Admin).ShouldBeFalse();
    }

    [Fact]
    public void Outside_a_request_everything_is_empty_rather_than_throwing()
    {
        var currentUser = new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = null });

        currentUser.IsAuthenticated.ShouldBeFalse();
        currentUser.UserId.ShouldBeNull();
        currentUser.Roles.ShouldBeEmpty();
        currentUser.IpAddress.ShouldBeNull();
    }

    [Fact]
    public void An_unparseable_subject_claim_reads_as_no_user_rather_than_a_wrong_one()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "not-a-guid")], "TestAuth");

        For(new ClaimsPrincipal(identity)).UserId.ShouldBeNull();
    }

    [Fact]
    public void The_caller_ip_is_available_for_the_audit_trail()
    {
        var httpContext = new DefaultHttpContext { User = Authenticated(UserId, "Dana Okafor", ItmsRoles.Admin) };
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.4.2.9");

        new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = httpContext })
            .IpAddress.ShouldBe("10.4.2.9");
    }

    [Fact]
    public void The_role_names_are_the_three_the_architecture_allows()
    {
        ItmsRoles.All.ShouldBe(["Admin", "Technician", "User"]);
    }

    private static ClaimsPrincipal Authenticated(Guid id, string name, params string[] roles)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(ClaimTypes.Name, name),
            .. roles.Select(role => new Claim(ClaimTypes.Role, role)),
        ];

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static HttpContextCurrentUser For(ClaimsPrincipal principal) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });
}
