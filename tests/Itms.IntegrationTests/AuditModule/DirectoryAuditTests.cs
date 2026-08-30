using System.Net;
using Itms.IntegrationTests.DirectoryModule;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.AuditModule;

/// <summary>
/// <c>IAuditWriter</c> proved through real handlers rather than a double.
/// </summary>
/// <remarks>
/// Departments and locations raise no domain event, so they are the "mutations that do
/// not warrant a domain event" ARCHITECTURE.md §8 keeps the writer for, and SPEC.md §15
/// counts them as the administrative changes that are mandatory coverage. They are also
/// the only handlers in the build so far that can demonstrate the actor, the address, and
/// the transaction behaviour on a path a person actually walks.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class DirectoryAuditTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Creating_a_department_records_the_actor_the_address_and_the_fields()
    {
        var (admin, adminId) = await SignedInAsync("admin");
        using var client = admin;

        var department = await DirectoryClient.CreateDepartmentAsync(client, "Finance", "FIN", Token);

        var row = (await Entries("Department", department.Id.ToString())).ShouldHaveSingleItem();

        row.Action.ShouldBe("directory.department_created");
        row.ActorId.ShouldBe(adminId);
        row.ActorName.ShouldNotBeNullOrWhiteSpace();
        row.SourceIp.ShouldNotBeNull();
        row.Changes["name"].ShouldBe(new(null, "Finance"));
        row.Changes["code"].ShouldBe(new(null, "FIN"));
    }

    [Fact]
    public async Task An_edit_records_only_the_fields_that_moved()
    {
        var (admin, _) = await SignedInAsync("admin");
        using var client = admin;

        var department = await DirectoryClient.CreateDepartmentAsync(client, "Finance", "FIN", Token);

        var response = await DirectoryClient.SendAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/departments/{department.Id}",
            new { name = "Finance & Operations", code = "FIN", description = (string?)null },
            Token);
        response.EnsureSuccessStatusCode();

        var updated = (await Entries("Department", department.Id.ToString()))
            .Single(row => row.Action == "directory.department_updated");

        // The code was posted unchanged. An entry that claimed it changed would make every
        // edit look like a rewrite of the whole row.
        updated.Changes.Keys.ShouldBe(["name"]);
        updated.Changes["name"].ShouldBe(new("Finance", "Finance & Operations"));
    }

    [Fact]
    public async Task Retiring_and_reinstating_a_department_are_separate_actions()
    {
        var (admin, _) = await SignedInAsync("admin");
        using var client = admin;

        var department = await DirectoryClient.CreateDepartmentAsync(client, "Facilities", null, Token);

        await Status(client, department.Id, isActive: false);
        await Status(client, department.Id, isActive: true);

        var actions = (await Entries("Department", department.Id.ToString())).Select(row => row.Action);

        actions.ShouldBe([
            "directory.department_created",
            "directory.department_retired",
            "directory.department_reinstated",
        ]);
    }

    [Fact]
    public async Task Setting_the_state_a_department_already_has_records_nothing()
    {
        var (admin, _) = await SignedInAsync("admin");
        using var client = admin;

        var department = await DirectoryClient.CreateDepartmentAsync(client, "Facilities", null, Token);

        await Status(client, department.Id, isActive: true);

        // Nothing moved, so there is nothing to record. An entry here would be noise in
        // the one table that has to stay readable.
        (await Entries("Department", department.Id.ToString())).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Moving_a_location_records_the_parent_and_the_path_it_moved_between()
    {
        var (admin, _) = await SignedInAsync("admin");
        using var client = admin;

        var org = await DirectoryClient.CreateLocationAsync(client, "Acme", "Organization", null, Token);
        var siteA = await DirectoryClient.CreateLocationAsync(client, "North Site", "Site", org.Id, Token);
        var siteB = await DirectoryClient.CreateLocationAsync(client, "South Site", "Site", org.Id, Token);
        var building = await DirectoryClient.CreateLocationAsync(client, "Block A", "Building", siteA.Id, Token);

        var response = await DirectoryClient.SendAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/locations/{building.Id}/move",
            new { parentId = siteB.Id },
            Token);
        response.EnsureSuccessStatusCode();

        var moved = (await Entries("Location", building.Id.ToString()))
            .Single(row => row.Action == "directory.location_moved");

        moved.Changes["parentId"].ShouldBe(new(siteA.Id.ToString(), siteB.Id.ToString()));
        moved.Changes["fullPath"].Before.ShouldBe("Acme / North Site / Block A");
        moved.Changes["fullPath"].After.ShouldBe("Acme / South Site / Block A");
    }

    [Fact]
    public async Task Deleting_a_location_leaves_the_only_remaining_record_of_what_was_there()
    {
        var (admin, _) = await SignedInAsync("admin");
        using var client = admin;

        var org = await DirectoryClient.CreateLocationAsync(client, "Acme", "Organization", null, Token);
        var site = await DirectoryClient.CreateLocationAsync(client, "North Site", "Site", org.Id, Token);

        var response = await DirectoryClient.SendAsync(
            client, HttpMethod.Delete, $"/api/v1/locations/{site.Id}", null, Token);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var deleted = (await Entries("Location", site.Id.ToString()))
            .Single(row => row.Action == "directory.location_deleted");

        deleted.Changes["name"].ShouldBe(new("North Site", null));
        deleted.Changes["fullPath"].ShouldBe(new("Acme / North Site", null));
    }

    /// <summary>
    /// The transaction claim, tested from the failing side: a refused create must leave no
    /// entry saying it happened.
    /// </summary>
    [Fact]
    public async Task A_refused_change_writes_no_audit_row()
    {
        var (admin, _) = await SignedInAsync("admin");
        using var client = admin;

        await DirectoryClient.CreateDepartmentAsync(client, "Finance", "FIN", Token);

        var duplicate = await DirectoryClient.SendAsync(
            client,
            HttpMethod.Post,
            "/api/v1/departments",
            new { name = "finance", code = (string?)null, description = (string?)null },
            Token);

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var created = await AuditQueries.ByActionAsync(fixture.DataSource, "directory.department_created", Token);
        created.Count.ShouldBe(1);
    }

    private Task<IReadOnlyList<AuditRow>> Entries(string entityType, string entityId) =>
        AuditQueries.ByEntityAsync(fixture.DataSource, entityType, entityId, Token);

    private static async Task Status(HttpClient client, Guid departmentId, bool isActive)
    {
        var response = await DirectoryClient.SendAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/departments/{departmentId}/{(isActive ? "reactivate" : "deactivate")}",
            null,
            Token);

        response.EnsureSuccessStatusCode();
    }

    private async Task<(HttpClient Client, Guid UserId)> SignedInAsync(string userName)
    {
        var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        var user = await AuthClient.ReadUserAsync(response, Token);
        return (client, user.Id);
    }
}
