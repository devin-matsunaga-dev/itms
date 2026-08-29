using System.Net;

namespace Itms.IntegrationTests.Identity;

/// <summary>
/// ARCHITECTURE.md §7: authorization is policy-based and evaluated server-side on every
/// endpoint. The user directory is guarded by the Technician policy, so it is where the
/// role matrix can be asserted over the wire rather than in configuration.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class RoleAuthorizationTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData("tech")]
    // Admin satisfies the Technician policy: SPEC.md §14 gives Admin the superset.
    [InlineData("admin")]
    public async Task A_technician_or_admin_may_read_the_user_directory(string userName)
    {
        using var client = fixture.CreateClient();
        await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);

        var response = await client.GetAsync(new Uri("/api/v1/users?search=a", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_end_user_is_forbidden_from_the_user_directory()
    {
        using var client = fixture.CreateClient();
        await AuthClient.LoginAsync(client, "user", AuthClient.Password, Token);

        var response = await client.GetAsync(new Uri("/api/v1/users?search=a", UriKind.Relative), Token);

        // 403, not a 404 disguise and not a redirect (ARCHITECTURE.md §6).
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Headers.Location.ShouldBeNull();
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task An_anonymous_caller_is_challenged_rather_than_forbidden()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/users?search=a", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_directory_returns_no_credential_state()
    {
        using var client = fixture.CreateClient();
        await AuthClient.LoginAsync(client, "tech", AuthClient.Password, Token);

        var body = await client.GetStringAsync(new Uri("/api/v1/users?search=a", UriKind.Relative), Token);

        // IUserLookup carries a summary and nothing else; this is the assertion that
        // notices if someone widens UserSummary later.
        body.ShouldNotContain("passwordHash", Case.Insensitive);
        body.ShouldNotContain("securityStamp", Case.Insensitive);
        body.ShouldNotContain("lockout", Case.Insensitive);
    }
}
