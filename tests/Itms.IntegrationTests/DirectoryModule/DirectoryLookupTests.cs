using Itms.Contracts.Lookups;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Directory.Seeding;
using Microsoft.Extensions.DependencyInjection;

namespace Itms.IntegrationTests.DirectoryModule;

/// <summary>
/// The two public contracts this module implements. ARCHITECTURE.md §3 rule 2 makes
/// these the only way another module reads a department or a location, so the shape they
/// return is a boundary that has to be asserted rather than assumed.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class DirectoryLookupTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Invariant 7 requires an alert to keep the location context it was raised with, and
    /// this is where that context comes from: the summary carries the whole path, not an
    /// id the caller would have to resolve later.
    /// </summary>
    [Fact]
    public async Task A_location_summary_carries_the_full_path()
    {
        using var admin = await SignedInAsync("admin");
        var root = await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);
        var site = await DirectoryClient.CreateLocationAsync(admin, "Head Office", "Site", root.Id, Token);
        var room = await DirectoryClient.CreateLocationAsync(admin, "Server Room G-04", "Room", site.Id, Token);

        await using var scope = fixture.Services.CreateAsyncScope();
        var lookup = scope.ServiceProvider.GetRequiredService<ILocationLookup>();

        var summary = await lookup.GetAsync(room.Id, Token);

        summary.ShouldNotBeNull();
        summary.Name.ShouldBe("Server Room G-04");
        summary.Path.ShouldBe("Northvale Utilities / Head Office / Server Room G-04");
        summary.ParentId.ShouldBe(site.Id);
    }

    [Fact]
    public async Task An_unknown_location_resolves_to_null_rather_than_throwing()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var lookup = scope.ServiceProvider.GetRequiredService<ILocationLookup>();

        (await lookup.GetAsync(Guid.CreateVersion7(), Token)).ShouldBeNull();
    }

    [Fact]
    public async Task Locations_are_read_in_one_batch_for_a_list_screen()
    {
        using var admin = await SignedInAsync("admin");
        var root = await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);
        var siteA = await DirectoryClient.CreateLocationAsync(admin, "Head Office", "Site", root.Id, Token);
        var siteB = await DirectoryClient.CreateLocationAsync(admin, "Riverside", "Site", root.Id, Token);

        await using var scope = fixture.Services.CreateAsyncScope();
        var lookup = scope.ServiceProvider.GetRequiredService<ILocationLookup>();

        // A duplicate and an unknown id in the same call: the batch returns what exists.
        var summaries = await lookup.GetManyAsync([siteA.Id, siteB.Id, siteA.Id, Guid.CreateVersion7()], Token);

        summaries.Count.ShouldBe(2);
        summaries.Select(summary => summary.Id).ShouldBe([siteA.Id, siteB.Id], ignoreOrder: true);

        (await lookup.GetManyAsync([], Token)).ShouldBeEmpty();
    }

    /// <summary>
    /// A retired department still resolves. A ticket raised against it two years ago has
    /// to render, and a lookup that returned null would leave the caller with an id and
    /// nothing to show.
    /// </summary>
    [Fact]
    public async Task A_retired_department_still_resolves_with_IsActive_false()
    {
        using var admin = await SignedInAsync("admin");
        var department = await DirectoryClient.CreateDepartmentAsync(admin, "Typing Pool", null, Token);
        await DirectoryClient.SendAsync(
            admin, HttpMethod.Post, $"/api/v1/departments/{department.Id}/deactivate", null, Token);

        await using var scope = fixture.Services.CreateAsyncScope();
        var lookup = scope.ServiceProvider.GetRequiredService<IDepartmentLookup>();

        var summary = await lookup.GetAsync(department.Id, Token);

        summary.ShouldNotBeNull();
        summary.Name.ShouldBe("Typing Pool");
        summary.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Departments_are_read_in_one_batch_for_a_list_screen()
    {
        using var admin = await SignedInAsync("admin");
        var finance = await DirectoryClient.CreateDepartmentAsync(admin, "Finance", "FIN", Token);
        var operations = await DirectoryClient.CreateDepartmentAsync(admin, "Operations", "OPS", Token);

        await using var scope = fixture.Services.CreateAsyncScope();
        var lookup = scope.ServiceProvider.GetRequiredService<IDepartmentLookup>();

        var summaries = await lookup.GetManyAsync([finance.Id, operations.Id], Token);

        summaries.Select(summary => summary.Name).ShouldBe(["Finance", "Operations"], ignoreOrder: true);
    }

    /// <summary>
    /// A rename has to reach the contract too, or another module would keep rendering a
    /// path that no longer exists.
    /// </summary>
    [Fact]
    public async Task A_renamed_ancestor_is_reflected_in_a_descendants_summary()
    {
        using var admin = await SignedInAsync("admin");
        var root = await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);
        var site = await DirectoryClient.CreateLocationAsync(admin, "Head Office", "Site", root.Id, Token);
        var room = await DirectoryClient.CreateLocationAsync(admin, "Reception", "Room", site.Id, Token);

        await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"/api/v1/locations/{site.Id}",
            new { name = "Head Office North", description = (string?)null },
            Token);

        await using var scope = fixture.Services.CreateAsyncScope();
        var lookup = scope.ServiceProvider.GetRequiredService<ILocationLookup>();

        (await lookup.GetAsync(room.Id, Token))!.Path
            .ShouldBe("Northvale Utilities / Head Office North / Reception");
    }

    /// <summary>
    /// The development seeder, which is what makes <c>aspire run</c> the only setup step.
    /// It is not run by <c>ResetAsync</c>, so this is the only place it is exercised.
    /// </summary>
    [Fact]
    public async Task The_development_seeder_builds_the_tree_and_is_safe_to_run_twice()
    {
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await DevelopmentDirectorySeeder.SeedAsync(scope.ServiceProvider, Token);
        }

        using var admin = await SignedInAsync("admin");

        var departments = await admin.GetAsync(new Uri("/api/v1/departments?pageSize=200", UriKind.Relative), Token);
        var departmentPage = await DirectoryClient.ReadAsync<PageDto<DepartmentDto>>(departments, Token);
        departmentPage.Total.ShouldBe(5);

        var locations = await admin.GetAsync(new Uri("/api/v1/locations?pageSize=200", UriKind.Relative), Token);
        var locationPage = await DirectoryClient.ReadAsync<PageDto<LocationDto>>(locations, Token);
        locationPage.Total.ShouldBe(16);

        locationPage.Items.Select(item => item.Path)
            .ShouldContain("Northvale Utilities / Head Office / Admin Building / Ground Floor / Server Room G-04");

        // The pump station: a room directly under a site, with no building between them.
        locationPage.Items.Select(item => item.Path)
            .ShouldContain("Northvale Utilities / Kestrel Pump Station / Cabinet A");

        // Running it again adds nothing — `aspire run` re-seeds on every start.
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await DevelopmentDirectorySeeder.SeedAsync(scope.ServiceProvider, Token);
        }

        var after = await admin.GetAsync(new Uri("/api/v1/locations?pageSize=200", UriKind.Relative), Token);
        (await DirectoryClient.ReadAsync<PageDto<LocationDto>>(after, Token)).Total.ShouldBe(16);
    }

    private async Task<HttpClient> SignedInAsync(string userName)
    {
        var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        return client;
    }
}
