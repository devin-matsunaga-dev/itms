using System.Net;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.AuditModule;

/// <summary>
/// WP-0.7's stated criterion: logging in and failing to log in both write rows.
/// </summary>
/// <remarks>
/// A sign-in is the one audited action that changes nothing, so nothing but the audit
/// row records that it happened at all — which is why the failure cases matter more here
/// than the success case does.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class LoginAuditTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private const string Succeeded = "auth.login_succeeded";
    private const string Failed = "auth.login_failed";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_successful_sign_in_writes_a_row_naming_the_account_and_its_session()
    {
        using var client = fixture.CreateClient();

        var response = await AuthClient.LoginAsync(client, "admin", AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        var user = await AuthClient.ReadUserAsync(response, Token);

        var rows = await AuditQueries.ByActionAsync(fixture.DataSource, Succeeded, Token);

        var row = rows.ShouldHaveSingleItem();
        row.EntityType.ShouldBe("User");
        row.EntityId.ShouldBe(user.Id.ToString());
        row.Changes["sessionId"].After.ShouldNotBeNullOrWhiteSpace();
        row.SourceIp.ShouldNotBeNull();
    }

    /// <summary>
    /// The request that signs somebody in is itself anonymous — the principal does not
    /// exist until it succeeds — so the actor column is null and the account is named by
    /// the entity instead. Asserted rather than left implicit, because a viewer asking
    /// "everything this person did" has to know to look both ways.
    /// </summary>
    [Fact]
    public async Task A_sign_in_names_the_account_as_the_entity_rather_than_as_the_actor()
    {
        using var client = fixture.CreateClient();
        (await AuthClient.LoginAsync(client, "tech", AuthClient.Password, Token)).EnsureSuccessStatusCode();

        var row = (await AuditQueries.ByActionAsync(fixture.DataSource, Succeeded, Token)).ShouldHaveSingleItem();

        row.ActorId.ShouldBeNull();
        row.EntityId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task A_wrong_password_writes_a_row_against_the_real_account()
    {
        using var client = fixture.CreateClient();
        var signedIn = await AuthClient.LoginAsync(client, "admin", AuthClient.Password, Token);
        var admin = await AuthClient.ReadUserAsync(signedIn, Token);

        using var attacker = fixture.CreateClient();
        var refused = await AuthClient.LoginAsync(attacker, "admin", "not-the-password", Token);
        refused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var row = (await AuditQueries.ByActionAsync(fixture.DataSource, Failed, Token)).ShouldHaveSingleItem();

        row.EntityId.ShouldBe(admin.Id.ToString());
        row.Changes["reason"].After.ShouldBe("bad password");
        row.Changes["userName"].After.ShouldBe("admin");
    }

    [Fact]
    public async Task A_sign_in_against_an_account_that_does_not_exist_records_what_was_tried()
    {
        using var client = fixture.CreateClient();

        var refused = await AuthClient.LoginAsync(client, "ghost@example.invalid", "whatever", Token);
        refused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var row = (await AuditQueries.ByActionAsync(fixture.DataSource, Failed, Token)).ShouldHaveSingleItem();

        // Enumeration is the thing this is here to make visible: a run of failures against
        // names that do not exist looks like nothing at all without it.
        row.EntityId.ShouldBe("ghost@example.invalid");
        row.Changes["reason"].After.ShouldBe("no such account");
        row.ActorId.ShouldBeNull();
        row.SourceIp.ShouldNotBeNull();
    }

    [Fact]
    public async Task No_audit_row_ever_carries_the_password_that_was_tried()
    {
        using var client = fixture.CreateClient();
        await AuthClient.LoginAsync(client, "admin", "hunter2-should-never-be-stored", Token);
        await AuthClient.LoginAsync(client, "admin", AuthClient.Password, Token);

        var rows = await AuditQueries.AllAsync(fixture.DataSource, Token);

        rows.ShouldNotBeEmpty();
        rows.SelectMany(r => r.Changes.Values)
            .SelectMany(c => new[] { c.Before, c.After })
            .ShouldAllBe(value => value == null || !value.Contains("hunter2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_repeated_failure_writes_one_row_per_attempt()
    {
        using var client = fixture.CreateClient();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await AuthClient.LoginAsync(client, "user", $"wrong-{attempt}", Token);
        }

        var rows = await AuditQueries.ByActionAsync(fixture.DataSource, Failed, Token);

        // Three separate refusals, not one collapsed entry: the count is the signal.
        rows.Count.ShouldBe(3);
    }
}
