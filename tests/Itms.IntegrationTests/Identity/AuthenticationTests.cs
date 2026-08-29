using System.Net;
using System.Net.Http.Json;
using Itms.Modules.Identity.Domain;
using Itms.Platform.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Itms.IntegrationTests.Identity;

/// <summary>
/// WP-0.5's acceptance criteria, asserted over HTTP against the real host.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class AuthenticationTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string WithoutTraceId(string body) =>
        System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\":\"[^\"]*\"", "\"traceId\":\"*\"");

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData("admin", ItmsRoles.Admin)]
    [InlineData("tech", ItmsRoles.Technician)]
    [InlineData("user", ItmsRoles.User)]
    public async Task Each_seeded_role_can_sign_in_and_is_reported_by_me(string userName, string expectedRole)
    {
        using var client = fixture.CreateClient();

        var login = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var me = await AuthClient.MeAsync(client, Token);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);

        var account = await AuthClient.ReadUserAsync(me, Token);
        account.UserName.ShouldBe(userName);
        account.Roles.ShouldHaveSingleItem().ShouldBe(expectedRole);
    }

    [Fact]
    public async Task Signing_in_by_email_works_as_well_as_by_user_name()
    {
        using var client = fixture.CreateClient();

        var login = await AuthClient.LoginAsync(client, "tech@itms.local", AuthClient.Password, Token);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unauthenticated_call_to_a_protected_endpoint_is_401_with_a_problem_document()
    {
        using var client = fixture.CreateClient();

        var response = await AuthClient.MeAsync(client, Token);

        // The whole point: not a 302 to an HTML login page, which is what the cookie
        // handler does by default and what would leave a fetch call parsing markup.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Location.ShouldBeNull();
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var body = await response.Content.ReadAsStringAsync(Token);
        body.ShouldNotContain("<html", Case.Insensitive);
    }

    [Fact]
    public async Task A_wrong_password_is_401_with_the_generic_code()
    {
        using var client = fixture.CreateClient();

        var response = await AuthClient.LoginAsync(client, "admin", "NotTheRightOne!1", Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(Token)).ShouldContain("auth.invalid_credentials");
    }

    [Fact]
    public async Task An_unknown_account_is_answered_exactly_like_a_wrong_password()
    {
        using var client = fixture.CreateClient();

        var unknown = await AuthClient.LoginAsync(client, "nobody", AuthClient.Password, Token);
        var wrong = await AuthClient.LoginAsync(client, "admin", "NotTheRightOne!1", Token);

        unknown.StatusCode.ShouldBe(wrong.StatusCode);

        // Compared without the trace id, which is per-request by design. Everything else
        // has to match, or the login form becomes a way to ask whether an account exists.
        WithoutTraceId(await unknown.Content.ReadAsStringAsync(Token))
            .ShouldBe(WithoutTraceId(await wrong.Content.ReadAsStringAsync(Token)));
    }

    [Fact]
    public async Task The_session_cookie_is_http_only_secure_and_same_site_lax()
    {
        using var client = fixture.CreateClient();

        var login = await AuthClient.LoginAsync(client, "admin", AuthClient.Password, Token);

        var cookie = login.Headers
            .GetValues("Set-Cookie")
            .Single(value => value.StartsWith("itms.session=", StringComparison.Ordinal));

        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("secure", Case.Insensitive);
        cookie.ShouldContain("samesite=lax", Case.Insensitive);

        // ARCHITECTURE.md §7: no token in the browser. The body carries the account, and
        // nothing the client could be tempted to put in localStorage.
        var body = await login.Content.ReadAsStringAsync(Token);
        body.ShouldNotContain("token", Case.Insensitive);
        body.ShouldNotContain(".AspNetCore.", Case.Sensitive);
    }

    [Fact]
    public async Task A_post_without_an_antiforgery_token_is_rejected()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new { userName = "admin", password = AuthClient.Password },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Token)).ShouldContain("auth.antiforgery_failed");
    }

    [Fact]
    public async Task Lockout_engages_after_the_configured_number_of_failures()
    {
        using var client = fixture.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await AuthClient.LoginAsync(client, "user", "WrongPassword!1", Token);
        }

        // Even the correct password now fails: lockout is about the account, not the guess.
        var locked = await AuthClient.LoginAsync(client, "user", AuthClient.Password, Token);

        locked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await locked.Content.ReadAsStringAsync(Token)).ShouldContain("auth.locked_out");
    }

    [Fact]
    public async Task Logging_out_revokes_the_session_immediately()
    {
        using var client = fixture.CreateClient();
        await AuthClient.LoginAsync(client, "admin", AuthClient.Password, Token);

        var logout = await AuthClient.PostAsync(client, "/api/v1/auth/logout", body: null, Token);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterwards = await AuthClient.MeAsync(client, Token);
        afterwards.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_revoked_session_stops_working_even_when_the_cookie_is_replayed()
    {
        // Two clients are two browsers holding two cookies for the same account.
        using var first = fixture.CreateClient();
        using var second = fixture.CreateClient();

        await AuthClient.LoginAsync(first, "tech", AuthClient.Password, Token);
        await AuthClient.LoginAsync(second, "tech", AuthClient.Password, Token);

        var change = await AuthClient.PostAsync(
            second,
            "/api/v1/auth/change-password",
            new { currentPassword = AuthClient.Password, newPassword = "AnotherDev!Pass9" },
            Token);

        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The browser that changed the password keeps working; the other one does not,
        // which is the entire point of revoking on a password change.
        (await AuthClient.MeAsync(second, Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AuthClient.MeAsync(first, Token)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_password_change_with_the_wrong_current_password_is_rejected_per_field()
    {
        using var client = fixture.CreateClient();
        await AuthClient.LoginAsync(client, "admin", AuthClient.Password, Token);

        var response = await AuthClient.PostAsync(
            client,
            "/api/v1/auth/change-password",
            new { currentPassword = "NotTheRightOne!1", newPassword = "AnotherDev!Pass9" },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Token)).ShouldContain("currentPassword");
    }

    [Fact]
    public async Task A_new_password_below_the_policy_is_rejected()
    {
        using var client = fixture.CreateClient();
        await AuthClient.LoginAsync(client, "admin", AuthClient.Password, Token);

        var response = await AuthClient.PostAsync(
            client,
            "/api/v1/auth/change-password",
            new { currentPassword = AuthClient.Password, newPassword = "short1!A" },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Token)).ShouldContain("newPassword");
    }

    [Fact]
    public async Task Deactivating_a_user_kills_their_live_session_on_the_next_request()
    {
        using var client = fixture.CreateClient();
        await AuthClient.LoginAsync(client, "user", AuthClient.Password, Token);
        (await AuthClient.MeAsync(client, Token)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ItmsUser>>();
            var account = await users.FindByNameAsync("user");
            account!.Deactivate(DateTimeOffset.UtcNow, actor: null);
            (await users.UpdateAsync(account)).Succeeded.ShouldBeTrue();
        }

        (await AuthClient.MeAsync(client, Token)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
