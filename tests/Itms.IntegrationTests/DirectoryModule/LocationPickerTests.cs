using System.Net;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.DirectoryModule;

/// <summary>
/// The reads a cascading location picker is built on: the top level, the chain down to a
/// node the picker was opened on, and the filter that stops it offering a parent the
/// server would refuse.
/// </summary>
/// <remarks>
/// The picker itself is WP-2.7. What is asserted here is that a client can build one
/// without walking the tree a request at a time and without re-implementing the hierarchy
/// rule in TypeScript.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class LocationPickerTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// The first select of the picker. <c>GET /locations</c> cannot answer this — its
    /// <c>parentId</c> is a filter, and omitting it lists the whole tree.
    /// </summary>
    [Fact]
    public async Task The_roots_read_returns_only_the_top_level()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);
        var second = await DirectoryClient.CreateLocationAsync(admin, "Argyle Water", "Organization", null, Token);

        var roots = await ListAsync(admin, "/api/v1/locations/roots");

        roots.Total.ShouldBe(2);
        roots.Items.Select(item => item.Id).ShouldBe([second.Id, tree.Root.Id], ignoreOrder: true);
        roots.Items.ShouldAllBe(item => item.ParentId == null);

        // The whole tree is six nodes now; the roots read is the two organisations.
        (await ListAsync(admin, "/api/v1/locations?pageSize=200")).Total.ShouldBe(6);
    }

    /// <summary>Ordered by name, because a top level is a list of organisations rather than a queue.</summary>
    [Fact]
    public async Task The_roots_read_is_ordered_by_name()
    {
        using var admin = await SignedInAsync("admin");
        await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);
        await DirectoryClient.CreateLocationAsync(admin, "Argyle Water", "Organization", null, Token);
        await DirectoryClient.CreateLocationAsync(admin, "Meridian Power", "Organization", null, Token);

        var roots = await ListAsync(admin, "/api/v1/locations/roots");

        roots.Items.Select(item => item.Name).ShouldBe(["Argyle Water", "Meridian Power", "Northvale Utilities"]);
    }

    [Fact]
    public async Task The_roots_read_can_be_searched_and_paged()
    {
        using var admin = await SignedInAsync("admin");
        await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);
        await DirectoryClient.CreateLocationAsync(admin, "Argyle Water", "Organization", null, Token);

        var matched = await ListAsync(admin, "/api/v1/locations/roots?search=argyle");
        matched.Total.ShouldBe(1);
        matched.Items[0].Name.ShouldBe("Argyle Water");

        var firstPage = await ListAsync(admin, "/api/v1/locations/roots?pageSize=1");
        firstPage.Total.ShouldBe(2);
        firstPage.Items.Count.ShouldBe(1);
    }

    /// <summary>
    /// The whole point of the endpoint: a picker handed a room fills every level from one
    /// response, rather than one request per level with the deepest node costing most.
    /// </summary>
    [Fact]
    public async Task The_ancestor_chain_runs_root_first_and_includes_the_node_itself()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var chain = await ChainAsync(admin, tree.Room.Id);

        chain.Select(node => node.Id).ShouldBe([tree.Root.Id, tree.Site.Id, tree.Building.Id, tree.Floor.Id, tree.Room.Id]);
        chain.Select(node => node.Depth).ShouldBe([0, 1, 2, 3, 4]);
        chain[^1].Path.ShouldBe("Northvale Utilities / Head Office / Admin Building / Ground Floor / Server Room G-04");
    }

    [Fact]
    public async Task A_roots_own_chain_is_itself_alone_rather_than_empty()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var chain = await ChainAsync(admin, tree.Root.Id);

        chain.Count.ShouldBe(1);
        chain[0].Id.ShouldBe(tree.Root.Id);
    }

    /// <summary>
    /// A move rewrites the id path the chain is derived from, so the chain has to follow
    /// the node to its new home rather than remembering where it used to hang.
    /// </summary>
    [Fact]
    public async Task The_chain_follows_a_node_that_has_been_moved()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);
        var riverside = await DirectoryClient.CreateLocationAsync(
            admin, "Riverside Treatment Plant", "Site", tree.Root.Id, Token);

        await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"/api/v1/locations/{tree.Building.Id}/move",
            new { parentId = riverside.Id },
            Token);

        var chain = await ChainAsync(admin, tree.Room.Id);

        chain.Select(node => node.Id)
            .ShouldBe([tree.Root.Id, riverside.Id, tree.Building.Id, tree.Floor.Id, tree.Room.Id]);
    }

    [Fact]
    public async Task An_ancestor_chain_for_a_location_that_does_not_exist_is_a_404()
    {
        using var admin = await SignedInAsync("admin");

        var response = await admin.GetAsync(
            new Uri($"/api/v1/locations/{Guid.CreateVersion7()}/ancestors", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await DirectoryClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("directory.location_not_found");
    }

    /// <summary>
    /// The filter that keeps the hierarchy rule on the server. Without it a picker either
    /// offers every node and lets the user discover the rule through a 409, or ships a
    /// second copy of <c>LocationHierarchy</c> that the two then disagree about.
    /// </summary>
    [Fact]
    public async Task Adoptable_for_offers_only_the_levels_that_could_hold_the_kind()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var forBuilding = await ListAsync(admin, "/api/v1/locations?adoptableFor=Building&pageSize=200");
        forBuilding.Items.Select(item => item.Id).ShouldBe([tree.Root.Id, tree.Site.Id], ignoreOrder: true);

        // A room may hang off anything above it, including a site with no building.
        var forRoom = await ListAsync(admin, "/api/v1/locations?adoptableFor=Room&pageSize=200");
        forRoom.Items.Select(item => item.Id)
            .ShouldBe([tree.Root.Id, tree.Site.Id, tree.Building.Id, tree.Floor.Id], ignoreOrder: true);

        // Nothing contains an organisation, so nothing is offered.
        (await ListAsync(admin, "/api/v1/locations?adoptableFor=Organization&pageSize=200")).Total.ShouldBe(0);
    }

    /// <summary>
    /// The filter's other half. A node deep enough that a child would breach the depth
    /// limit is excluded even though its kind ranks above the child's — which is the same
    /// pair of conditions <c>Location.CanAdopt</c> applies.
    /// </summary>
    [Fact]
    public async Task Adoptable_for_excludes_a_node_with_no_depth_left()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var forRoom = await ListAsync(admin, "/api/v1/locations?adoptableFor=Room&pageSize=200");

        // The floor sits at depth 3 and can still take a room at 4. The room itself sits
        // at 4 — a child would be at 5, which is the limit — and its rank forbids it too.
        forRoom.Items.Select(item => item.Id).ShouldContain(tree.Floor.Id);
        forRoom.Items.Select(item => item.Id).ShouldNotContain(tree.Room.Id);
    }

    /// <summary>
    /// Everything the filter offers, the server then accepts. This is the property that
    /// makes the filter worth having at all.
    /// </summary>
    [Fact]
    public async Task Every_parent_the_filter_offers_actually_accepts_the_child()
    {
        using var admin = await SignedInAsync("admin");
        await BuildTreeAsync(admin);

        var offered = await ListAsync(admin, "/api/v1/locations?adoptableFor=Room&pageSize=200");

        var index = 0;
        foreach (var parent in offered.Items)
        {
            var response = await DirectoryClient.SendAsync(
                admin,
                HttpMethod.Post,
                "/api/v1/locations",
                new { name = $"Store Room {index++}", kind = "Room", parentId = parent.Id, description = (string?)null },
                Token);

            response.StatusCode.ShouldBe(HttpStatusCode.Created, $"'{parent.Path}' was offered but refused.");
        }
    }

    [Fact]
    public async Task Adoptable_for_narrows_the_roots_read_as_well()
    {
        using var admin = await SignedInAsync("admin");
        await BuildTreeAsync(admin);

        // An organisation is the top of the tree, so a root can hold anything below it…
        (await ListAsync(admin, "/api/v1/locations/roots?adoptableFor=Site")).Total.ShouldBe(1);

        // …and never another organisation.
        (await ListAsync(admin, "/api/v1/locations/roots?adoptableFor=Organization")).Total.ShouldBe(0);
    }

    [Fact]
    public async Task Adoptable_for_combines_with_the_other_filters_by_narrowing()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var sitesThatCouldHoldARoom = await ListAsync(
            admin, "/api/v1/locations?adoptableFor=Room&kind=Site&pageSize=200");

        sitesThatCouldHoldARoom.Total.ShouldBe(1);
        sitesThatCouldHoldARoom.Items[0].Id.ShouldBe(tree.Site.Id);
    }

    /// <summary>
    /// Both picker reads sit under the same Authenticated policy as the rest of the
    /// reads: an end user filing a ticket has to say which room they are in.
    /// </summary>
    [Theory]
    [InlineData("tech")]
    [InlineData("user")]
    public async Task A_non_admin_may_use_the_picker_reads(string userName)
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        using var caller = await SignedInAsync(userName);

        (await caller.GetAsync(new Uri("/api/v1/locations/roots", UriKind.Relative), Token))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await caller.GetAsync(new Uri($"/api/v1/locations/{tree.Room.Id}/ancestors", UriKind.Relative), Token))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<HttpClient> SignedInAsync(string userName)
    {
        var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<PageDto<LocationDto>> ListAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Token);
        response.EnsureSuccessStatusCode();
        return await DirectoryClient.ReadAsync<PageDto<LocationDto>>(response, Token);
    }

    private static async Task<IReadOnlyList<LocationDto>> ChainAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync(new Uri($"/api/v1/locations/{id}/ancestors", UriKind.Relative), Token);
        response.EnsureSuccessStatusCode();
        return await DirectoryClient.ReadAsync<IReadOnlyList<LocationDto>>(response, Token);
    }

    private static async Task<Tree> BuildTreeAsync(HttpClient admin)
    {
        var root = await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);
        var site = await DirectoryClient.CreateLocationAsync(admin, "Head Office", "Site", root.Id, Token);
        var building = await DirectoryClient.CreateLocationAsync(admin, "Admin Building", "Building", site.Id, Token);
        var floor = await DirectoryClient.CreateLocationAsync(admin, "Ground Floor", "Floor", building.Id, Token);
        var room = await DirectoryClient.CreateLocationAsync(admin, "Server Room G-04", "Room", floor.Id, Token);

        return new Tree(root, site, building, floor, room);
    }

    private sealed record Tree(
        LocationDto Root,
        LocationDto Site,
        LocationDto Building,
        LocationDto Floor,
        LocationDto Room);
}
