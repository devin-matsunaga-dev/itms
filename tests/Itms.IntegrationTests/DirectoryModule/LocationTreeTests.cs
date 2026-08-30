using System.Net;
using Itms.IntegrationTests.Identity;
using Itms.TestSupport;
using Npgsql;

namespace Itms.IntegrationTests.DirectoryModule;

/// <summary>
/// The location tree over the wire: that it persists, that a full path costs one read,
/// that a rename or a move carries the subtree with it, and that deleting a node with
/// children is refused with something an operator can act on.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class LocationTreeTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task The_five_level_hierarchy_persists_with_its_full_path_and_depth()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        tree.Room.Path.ShouldBe("Northvale Utilities / Head Office / Admin Building / Ground Floor / Server Room G-04");
        tree.Room.Depth.ShouldBe(4);
        tree.Room.ParentId.ShouldBe(tree.Floor.Id);
        tree.Root.Depth.ShouldBe(0);
        tree.Root.ParentId.ShouldBeNull();
    }

    /// <summary>
    /// WP-0.6's "queries a full path efficiently". The path is materialised on the row,
    /// so reading a room five levels down is one indexed row read — not one query per
    /// ancestor, and not a recursive CTE.
    /// </summary>
    [Fact]
    public async Task A_full_path_is_read_from_the_row_itself_and_never_walked()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var stored = await ReadColumnAsync("full_path", tree.Room.Id);

        stored.ShouldBe("Northvale Utilities / Head Office / Admin Building / Ground Floor / Server Room G-04");

        var fetched = await GetAsync(admin, tree.Room.Id);
        fetched.Path.ShouldBe(stored);
    }

    /// <summary>
    /// The id path is what subtree queries match on, so every descendant's must extend
    /// its ancestor's. This is the invariant the prefix index and the rewrite both rest on.
    /// </summary>
    [Fact]
    public async Task Every_descendant_id_path_extends_its_ancestors()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var rootPath = await ReadColumnAsync("path", tree.Root.Id);
        var sitePath = await ReadColumnAsync("path", tree.Site.Id);
        var roomPath = await ReadColumnAsync("path", tree.Room.Id);

        sitePath.ShouldStartWith(rootPath);
        roomPath.ShouldStartWith(sitePath);
        roomPath.ShouldBe($"{sitePath}{tree.Building.Id:N}/{tree.Floor.Id:N}/{tree.Room.Id:N}/");
    }

    [Fact]
    public async Task Renaming_a_node_rewrites_the_display_path_of_everything_beneath_it()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"/api/v1/locations/{tree.Site.Id}",
            new { name = "Head Office North", description = (string?)null },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await GetAsync(admin, tree.Site.Id)).Path.ShouldBe("Northvale Utilities / Head Office North");
        (await GetAsync(admin, tree.Room.Id)).Path
            .ShouldBe("Northvale Utilities / Head Office North / Admin Building / Ground Floor / Server Room G-04");
        (await GetAsync(admin, tree.Building.Id)).Path
            .ShouldBe("Northvale Utilities / Head Office North / Admin Building");
    }

    /// <summary>
    /// A rename changes no id, so the descendants' id paths — and therefore their depths
    /// and their membership of the subtree — must come out untouched.
    /// </summary>
    [Fact]
    public async Task Renaming_leaves_the_id_paths_and_depths_alone()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);
        var before = await ReadColumnAsync("path", tree.Room.Id);

        await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"/api/v1/locations/{tree.Site.Id}",
            new { name = "Head Office North", description = (string?)null },
            Token);

        (await ReadColumnAsync("path", tree.Room.Id)).ShouldBe(before);
        (await GetAsync(admin, tree.Room.Id)).Depth.ShouldBe(4);
    }

    /// <summary>
    /// A sibling whose name merely starts with the renamed node's must not be dragged
    /// along — which is why the rewrite selects on the id path and not on the display one.
    /// </summary>
    [Fact]
    public async Task A_rename_does_not_touch_a_sibling_whose_path_shares_a_prefix()
    {
        using var admin = await SignedInAsync("admin");
        var root = await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);
        var head = await DirectoryClient.CreateLocationAsync(admin, "Head Office", "Site", root.Id, Token);
        var headAnnex = await DirectoryClient.CreateLocationAsync(admin, "Head Office Annex", "Site", root.Id, Token);
        var annexRoom = await DirectoryClient.CreateLocationAsync(admin, "Cabinet A", "Room", headAnnex.Id, Token);

        await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"/api/v1/locations/{head.Id}",
            new { name = "Head Office North", description = (string?)null },
            Token);

        (await GetAsync(admin, headAnnex.Id)).Path.ShouldBe("Northvale Utilities / Head Office Annex");
        (await GetAsync(admin, annexRoom.Id)).Path.ShouldBe("Northvale Utilities / Head Office Annex / Cabinet A");
    }

    [Fact]
    public async Task Moving_a_node_carries_its_subtree_and_recomputes_paths_and_depths()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);
        var riverside = await DirectoryClient.CreateLocationAsync(
            admin, "Riverside Treatment Plant", "Site", tree.Root.Id, Token);

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"/api/v1/locations/{tree.Building.Id}/move",
            new { parentId = riverside.Id },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await GetAsync(admin, tree.Building.Id)).Path
            .ShouldBe("Northvale Utilities / Riverside Treatment Plant / Admin Building");
        (await GetAsync(admin, tree.Room.Id)).Path
            .ShouldBe("Northvale Utilities / Riverside Treatment Plant / Admin Building / Ground Floor / Server Room G-04");

        // The move was sideways, so nothing changed depth.
        (await GetAsync(admin, tree.Room.Id)).Depth.ShouldBe(4);

        var buildingPath = await ReadColumnAsync("path", tree.Building.Id);
        (await ReadColumnAsync("path", tree.Room.Id)).ShouldStartWith(buildingPath);
    }

    [Fact]
    public async Task Moving_a_node_up_a_level_shifts_the_whole_subtrees_depth()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"/api/v1/locations/{tree.Building.Id}/move",
            new { parentId = tree.Root.Id },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await GetAsync(admin, tree.Building.Id)).Depth.ShouldBe(1);
        (await GetAsync(admin, tree.Floor.Id)).Depth.ShouldBe(2);
        (await GetAsync(admin, tree.Room.Id)).Depth.ShouldBe(3);
        (await GetAsync(admin, tree.Room.Id)).Path
            .ShouldBe("Northvale Utilities / Admin Building / Ground Floor / Server Room G-04");
    }

    [Fact]
    public async Task A_node_cannot_be_moved_beneath_its_own_descendant()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"/api/v1/locations/{tree.Site.Id}/move",
            new { parentId = tree.Room.Id },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await DirectoryClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("directory.location_cycle");
    }

    [Fact]
    public async Task A_node_cannot_be_moved_beneath_itself()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"/api/v1/locations/{tree.Site.Id}/move",
            new { parentId = tree.Site.Id },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await DirectoryClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("directory.location_cycle");
    }

    /// <summary>WP-0.6's named criterion.</summary>
    [Fact]
    public async Task Deleting_a_location_with_children_is_refused_with_a_message_that_says_why()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var response = await DirectoryClient.SendAsync(
            admin, HttpMethod.Delete, $"/api/v1/locations/{tree.Building.Id}", null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await DirectoryClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("directory.location_has_children");
        problem.Detail!.ShouldContain("Admin Building");
        problem.Detail!.ShouldContain("1 location");

        // Refused, not partially applied: the building and its subtree are still there.
        (await GetAsync(admin, tree.Building.Id)).Id.ShouldBe(tree.Building.Id);
        (await GetAsync(admin, tree.Room.Id)).Id.ShouldBe(tree.Room.Id);
    }

    [Fact]
    public async Task A_leaf_deletes_and_then_its_parent_can_be_deleted_too()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var deleteRoom = await DirectoryClient.SendAsync(
            admin, HttpMethod.Delete, $"/api/v1/locations/{tree.Room.Id}", null, Token);
        deleteRoom.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var gone = await admin.GetAsync(new Uri($"/api/v1/locations/{tree.Room.Id}", UriKind.Relative), Token);
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var deleteFloor = await DirectoryClient.SendAsync(
            admin, HttpMethod.Delete, $"/api/v1/locations/{tree.Floor.Id}", null, Token);
        deleteFloor.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task The_child_count_tells_a_caller_whether_a_delete_will_be_refused()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        (await GetAsync(admin, tree.Building.Id)).ChildCount.ShouldBe(1);
        (await GetAsync(admin, tree.Room.Id)).ChildCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_root_that_is_not_an_organization_is_refused()
    {
        using var admin = await SignedInAsync("admin");

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Post,
            "/api/v1/locations",
            new { name = "Orphan Site", kind = "Site", parentId = (Guid?)null, description = (string?)null },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await DirectoryClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("directory.illegal_root_kind");
    }

    [Fact]
    public async Task An_inverted_placement_is_refused()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Post,
            "/api/v1/locations",
            new { name = "Impossible Building", kind = "Building", parentId = tree.Room.Id, description = (string?)null },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await DirectoryClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("directory.illegal_placement");
        problem.Detail!.ShouldContain("Building cannot sit under a Room");
    }

    /// <summary>
    /// The pump-station shape SPEC.md §5 names: a site with a cabinet in it and no
    /// building or floor worth inventing.
    /// </summary>
    [Fact]
    public async Task A_level_may_be_skipped()
    {
        using var admin = await SignedInAsync("admin");
        var root = await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);
        var station = await DirectoryClient.CreateLocationAsync(admin, "Kestrel Pump Station", "Site", root.Id, Token);

        var cabinet = await DirectoryClient.CreateLocationAsync(admin, "Cabinet A", "Room", station.Id, Token);

        cabinet.Path.ShouldBe("Northvale Utilities / Kestrel Pump Station / Cabinet A");
        cabinet.Depth.ShouldBe(2);
    }

    [Fact]
    public async Task Two_siblings_cannot_share_a_name_but_two_cousins_can()
    {
        using var admin = await SignedInAsync("admin");
        var root = await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);
        var siteA = await DirectoryClient.CreateLocationAsync(admin, "Head Office", "Site", root.Id, Token);
        var siteB = await DirectoryClient.CreateLocationAsync(admin, "Riverside", "Site", root.Id, Token);

        await DirectoryClient.CreateLocationAsync(admin, "Reception", "Room", siteA.Id, Token);

        // Same name under a different parent is fine — every building has a reception.
        var cousin = await DirectoryClient.CreateLocationAsync(admin, "Reception", "Room", siteB.Id, Token);
        cousin.Path.ShouldBe("Northvale Utilities / Riverside / Reception");

        var duplicate = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Post,
            "/api/v1/locations",
            new { name = "reception", kind = "Room", parentId = siteA.Id, description = (string?)null },
            Token);

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await DirectoryClient.ReadAsync<ProblemDto>(duplicate, Token)).Code
            .ShouldBe("directory.duplicate_location_name");
    }

    /// <summary>
    /// Two roots have a null parent, and PostgreSQL's default would treat those nulls as
    /// distinct and let both through. The unique index declares them equal instead.
    /// </summary>
    [Fact]
    public async Task Two_roots_cannot_share_a_name()
    {
        using var admin = await SignedInAsync("admin");
        await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Post,
            "/api/v1/locations",
            new { name = "northvale utilities", kind = "Organization", parentId = (Guid?)null, description = (string?)null },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_move_onto_a_name_already_taken_by_a_sibling_is_refused()
    {
        using var admin = await SignedInAsync("admin");
        var root = await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);
        var siteA = await DirectoryClient.CreateLocationAsync(admin, "Head Office", "Site", root.Id, Token);
        var siteB = await DirectoryClient.CreateLocationAsync(admin, "Riverside", "Site", root.Id, Token);
        await DirectoryClient.CreateLocationAsync(admin, "Reception", "Room", siteA.Id, Token);
        var roomB = await DirectoryClient.CreateLocationAsync(admin, "Reception", "Room", siteB.Id, Token);

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"/api/v1/locations/{roomB.Id}/move",
            new { parentId = siteA.Id },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await DirectoryClient.ReadAsync<ProblemDto>(response, Token)).Code
            .ShouldBe("directory.duplicate_location_name");
    }

    [Fact]
    public async Task A_missing_parent_is_a_404_rather_than_a_conflict()
    {
        using var admin = await SignedInAsync("admin");

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Post,
            "/api/v1/locations",
            new { name = "Orphan", kind = "Room", parentId = Guid.CreateVersion7(), description = (string?)null },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await DirectoryClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("directory.parent_not_found");
    }

    [Fact]
    public async Task The_list_is_ordered_by_path_so_a_client_can_render_a_tree_from_it()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var response = await admin.GetAsync(new Uri("/api/v1/locations?pageSize=200", UriKind.Relative), Token);
        var page = await DirectoryClient.ReadAsync<PageDto<LocationDto>>(response, Token);

        page.Total.ShouldBe(5);
        page.Items.Select(item => item.Path).ShouldBe(page.Items.Select(item => item.Path).Order(StringComparer.Ordinal));
        page.Items[0].Id.ShouldBe(tree.Root.Id);
    }

    [Fact]
    public async Task The_list_can_be_narrowed_to_one_parent_or_one_subtree()
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        var children = await ListAsync(admin, $"/api/v1/locations?parentId={tree.Building.Id}");
        children.Total.ShouldBe(1);
        children.Items[0].Id.ShouldBe(tree.Floor.Id);

        // The subtree includes the root of that subtree itself.
        var subtree = await ListAsync(admin, $"/api/v1/locations?rootId={tree.Building.Id}&pageSize=200");
        subtree.Total.ShouldBe(3);

        var rooms = await ListAsync(admin, "/api/v1/locations?kind=Room");
        rooms.Total.ShouldBe(1);
    }

    [Theory]
    [InlineData("tech")]
    [InlineData("user")]
    public async Task A_non_admin_may_read_locations_but_not_change_them(string userName)
    {
        using var admin = await SignedInAsync("admin");
        var tree = await BuildTreeAsync(admin);

        using var caller = await SignedInAsync(userName);

        var read = await caller.GetAsync(new Uri($"/api/v1/locations/{tree.Room.Id}", UriKind.Relative), Token);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);

        var delete = await DirectoryClient.SendAsync(
            caller, HttpMethod.Delete, $"/api/v1/locations/{tree.Room.Id}", null, Token);
        delete.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var create = await DirectoryClient.SendAsync(
            caller,
            HttpMethod.Post,
            "/api/v1/locations",
            new { name = "Shadow Room", kind = "Room", parentId = tree.Floor.Id, description = (string?)null },
            Token);
        create.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> SignedInAsync(string userName)
    {
        var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<LocationDto> GetAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync(new Uri($"/api/v1/locations/{id}", UriKind.Relative), Token);
        response.EnsureSuccessStatusCode();
        return await DirectoryClient.ReadAsync<LocationDto>(response, Token);
    }

    private static async Task<PageDto<LocationDto>> ListAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Token);
        response.EnsureSuccessStatusCode();
        return await DirectoryClient.ReadAsync<PageDto<LocationDto>>(response, Token);
    }

    /// <summary>
    /// Reads a materialised column straight from the table. The API deliberately does not
    /// expose the id path, but the tests that prove the subtree rewrite have to see it.
    /// </summary>
    private static async Task<string> ReadColumnAsync(string column, Guid id)
    {
        await using var connection = new NpgsqlConnection(
            SharedPostgres.Instance.ConnectionStringFor(IdentityWebFixture.DatabaseName));
        await connection.OpenAsync(Token);

        await using var command = connection.CreateCommand();
        // The column name is one of two literals chosen by the test, never by input.
        command.CommandText = $"SELECT {column} FROM directory.locations WHERE id = @id";
        command.Parameters.AddWithValue("id", id);

        return (string)(await command.ExecuteScalarAsync(Token))!;
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
