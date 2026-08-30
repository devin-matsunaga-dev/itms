using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.AuditModule;

/// <summary>
/// WP-0.7's other stated criterion, from the database's side: the audit table has no
/// update or delete path — not even one written by hand.
/// </summary>
/// <remarks>
/// <see cref="Itms.ArchitectureTests"/> asserts that no such path exists in the code.
/// This asserts that it would not work if somebody added one, or typed it straight into
/// psql. Invariant 10 is worth more than the code that implements it.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class AuditAppendOnlyDatabaseTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    // PostgreSQL's restrict_violation. The trigger raises it deliberately, so a caller
    // can tell a refusal apart from a connection having gone away.
    private const string RestrictViolation = "23001";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_update_is_refused_by_the_database()
    {
        await ARowExistsAsync();

        var failure = await AuditQueries.AttemptAsync(
            fixture.DataSource,
            "UPDATE audit.audit_entries SET action = 'tampered'",
            Token);

        failure.ShouldNotBeNull();
        failure.SqlState.ShouldBe(RestrictViolation);
    }

    [Fact]
    public async Task A_delete_is_refused_by_the_database()
    {
        await ARowExistsAsync();

        var failure = await AuditQueries.AttemptAsync(
            fixture.DataSource,
            "DELETE FROM audit.audit_entries",
            Token);

        failure.ShouldNotBeNull();
        failure.SqlState.ShouldBe(RestrictViolation);
    }

    [Fact]
    public async Task A_refused_update_leaves_the_row_exactly_as_it_was()
    {
        var before = await ARowExistsAsync();

        await AuditQueries.AttemptAsync(
            fixture.DataSource,
            "UPDATE audit.audit_entries SET actor_id = NULL, action = 'tampered'",
            Token);

        var after = (await AuditQueries.AllAsync(fixture.DataSource, Token)).ShouldHaveSingleItem();

        after.ShouldBe(before);
    }

    /// <summary>Signs in, which is the cheapest way to put exactly one row in the table.</summary>
    private async Task<AuditRow> ARowExistsAsync()
    {
        using var client = fixture.CreateClient();
        (await AuthClient.LoginAsync(client, "admin", AuthClient.Password, Token)).EnsureSuccessStatusCode();

        return (await AuditQueries.AllAsync(fixture.DataSource, Token)).ShouldHaveSingleItem();
    }
}
