using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.AssetsModule;
using Itms.IntegrationTests.Helpdesk;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.DirectoryModule;

/// <summary>
/// WP-2.4's usage counts: what a directory entry still holds, and the refusal that stops
/// a location being deleted out from under the rows that name it.
/// </summary>
/// <remarks>
/// This is the one suite that exercises the fan-out across all three counters at once,
/// which is also the only place the wiring is visible: a module that implements
/// <c>IDirectoryUsageLookup</c> and forgets to register it looks exactly like a module
/// with nothing to count.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class DirectoryUsageTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Every registered counter reports, including the ones with nothing to say. An
    /// absent row and a zero are different answers, and only one of them means "asked".
    /// </summary>
    [Fact]
    public async Task An_unused_location_reports_every_counter_at_zero()
    {
        using var admin = await SignedInAsync("admin");
        var room = await RoomAsync(admin);

        var usage = await LocationUsageAsync(admin, room.Id);

        usage.TotalReferences.ShouldBe(0);
        usage.ChildCount.ShouldBe(0);
        usage.CanDelete.ShouldBeTrue();
        usage.References.Select(reference => reference.EntityName).ShouldBe(["assets", "tickets", "users"]);
        usage.References.ShouldAllBe(reference => reference.Count == 0);
    }

    [Fact]
    public async Task An_asset_in_a_room_is_counted_against_it()
    {
        using var admin = await SignedInAsync("admin");
        using var tech = await SignedInAsync("tech");
        var room = await RoomAsync(admin);
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        await AssetsClient.CreateDetailedAsync(
            tech, new { assetTag = "LAP-2400", assetTypeId = typeId, locationId = room.Id }, Token);
        await AssetsClient.CreateDetailedAsync(
            tech, new { assetTag = "LAP-2401", assetTypeId = typeId, locationId = room.Id }, Token);

        var usage = await LocationUsageAsync(admin, room.Id);

        Count(usage.References, "assets").ShouldBe(2);
        usage.TotalReferences.ShouldBe(2);
        usage.CanDelete.ShouldBeFalse();
    }

    /// <summary>
    /// The count is of exactly this node, not its subtree — which is right, because only
    /// a childless node can reach the delete at all.
    /// </summary>
    [Fact]
    public async Task An_asset_in_a_sibling_room_is_not_counted()
    {
        using var admin = await SignedInAsync("admin");
        using var tech = await SignedInAsync("tech");
        var root = await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);
        var site = await DirectoryClient.CreateLocationAsync(admin, "Head Office", "Site", root.Id, Token);
        var storeRoom = await DirectoryClient.CreateLocationAsync(admin, "Store Room", "Room", site.Id, Token);
        var serverRoom = await DirectoryClient.CreateLocationAsync(admin, "Server Room", "Room", site.Id, Token);

        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        await AssetsClient.CreateDetailedAsync(
            tech, new { assetTag = "LAP-2410", assetTypeId = typeId, locationId = storeRoom.Id }, Token);

        Count((await LocationUsageAsync(admin, storeRoom.Id)).References, "assets").ShouldBe(1);
        Count((await LocationUsageAsync(admin, serverRoom.Id)).References, "assets").ShouldBe(0);

        // The site above holds neither: the reference is to the room, not to its ancestors.
        Count((await LocationUsageAsync(admin, site.Id)).References, "assets").ShouldBe(0);
    }

    [Fact]
    public async Task A_departments_assets_tickets_and_people_are_all_counted()
    {
        using var admin = await SignedInAsync("admin");
        using var tech = await SignedInAsync("tech");

        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);

        await AssetsClient.CreateDetailedAsync(
            tech, new { assetTag = "LAP-2420", assetTypeId = typeId, departmentId }, Token);
        await TicketClient.CreateAsync(tech, reference, departmentId, "Laptop will not charge", Token);
        await TicketClient.CreateAsync(tech, reference, departmentId, "Printer jams on duplex", Token);

        var usage = await DepartmentUsageAsync(admin, departmentId);

        Count(usage.References, "assets").ShouldBe(1);
        Count(usage.References, "tickets").ShouldBe(2);
        usage.TotalReferences.ShouldBe(3);
        usage.Name.ShouldBe("Water Operations");
        usage.IsActive.ShouldBeTrue();
    }

    /// <summary>
    /// A ticket has no location column, so Helpdesk's counter answers zero rather than
    /// declining to answer — and it answers zero even when the department it is filed
    /// against has plenty against it.
    /// </summary>
    [Fact]
    public async Task Tickets_never_count_against_a_location()
    {
        using var admin = await SignedInAsync("admin");
        using var tech = await SignedInAsync("tech");

        var room = await RoomAsync(admin);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        await TicketClient.CreateAsync(tech, reference, departmentId, "Laptop will not charge", Token);

        Count((await LocationUsageAsync(admin, room.Id)).References, "tickets").ShouldBe(0);
        Count((await DepartmentUsageAsync(admin, departmentId)).References, "tickets").ShouldBe(1);
    }

    /// <summary>WP-2.4's refusal, and the reason the endpoint exists.</summary>
    [Fact]
    public async Task Deleting_a_location_something_still_references_is_refused_with_the_count()
    {
        using var admin = await SignedInAsync("admin");
        using var tech = await SignedInAsync("tech");
        var room = await RoomAsync(admin);
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        await AssetsClient.CreateDetailedAsync(
            tech, new { assetTag = "LAP-2430", assetTypeId = typeId, locationId = room.Id }, Token);
        await AssetsClient.CreateDetailedAsync(
            tech, new { assetTag = "LAP-2431", assetTypeId = typeId, locationId = room.Id }, Token);
        await AssetsClient.CreateDetailedAsync(
            tech, new { assetTag = "LAP-2432", assetTypeId = typeId, locationId = room.Id }, Token);

        var response = await DirectoryClient.SendAsync(
            admin, HttpMethod.Delete, $"/api/v1/locations/{room.Id}", null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await DirectoryClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("directory.location_in_use");
        problem.Detail!.ShouldContain("Server Room G-04");
        problem.Detail!.ShouldContain("3 assets");

        // Refused, not partially applied.
        (await admin.GetAsync(new Uri($"/api/v1/locations/{room.Id}", UriKind.Relative), Token))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Two refusals, two codes. "Empty the subtree" and "move the equipment" send an
    /// administrator to different places, so a client showing one message for both would
    /// be sending them to the wrong one half the time.
    /// </summary>
    [Fact]
    public async Task A_subtree_and_a_reference_are_refused_with_different_codes()
    {
        using var admin = await SignedInAsync("admin");
        using var tech = await SignedInAsync("tech");
        var root = await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);
        var site = await DirectoryClient.CreateLocationAsync(admin, "Head Office", "Site", root.Id, Token);
        var room = await DirectoryClient.CreateLocationAsync(admin, "Server Room G-04", "Room", site.Id, Token);

        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        await AssetsClient.CreateDetailedAsync(
            tech, new { assetTag = "LAP-2440", assetTypeId = typeId, locationId = room.Id }, Token);

        var withChildren = await DirectoryClient.SendAsync(
            admin, HttpMethod.Delete, $"/api/v1/locations/{site.Id}", null, Token);
        (await DirectoryClient.ReadAsync<ProblemDto>(withChildren, Token)).Code
            .ShouldBe("directory.location_has_children");

        var inUse = await DirectoryClient.SendAsync(
            admin, HttpMethod.Delete, $"/api/v1/locations/{room.Id}", null, Token);
        (await DirectoryClient.ReadAsync<ProblemDto>(inUse, Token)).Code.ShouldBe("directory.location_in_use");
    }

    /// <summary>
    /// The children check runs first, so a node that is both full and referenced names
    /// the subtree — which is the problem that has to be solved first anyway.
    /// </summary>
    [Fact]
    public async Task A_node_that_is_both_full_and_referenced_reports_its_children_first()
    {
        using var admin = await SignedInAsync("admin");
        using var tech = await SignedInAsync("tech");
        var root = await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);
        var site = await DirectoryClient.CreateLocationAsync(admin, "Head Office", "Site", root.Id, Token);
        await DirectoryClient.CreateLocationAsync(admin, "Server Room G-04", "Room", site.Id, Token);

        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        await AssetsClient.CreateDetailedAsync(
            tech, new { assetTag = "LAP-2450", assetTypeId = typeId, locationId = site.Id }, Token);

        var response = await DirectoryClient.SendAsync(
            admin, HttpMethod.Delete, $"/api/v1/locations/{site.Id}", null, Token);

        (await DirectoryClient.ReadAsync<ProblemDto>(response, Token)).Code
            .ShouldBe("directory.location_has_children");
    }

    /// <summary>
    /// The point of blocking rather than cascading: the reference survives, so once the
    /// equipment moves the room can go and nothing was orphaned in between.
    /// </summary>
    [Fact]
    public async Task Moving_the_equipment_out_lets_the_delete_through()
    {
        using var admin = await SignedInAsync("admin");
        using var tech = await SignedInAsync("tech");
        var root = await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);
        var site = await DirectoryClient.CreateLocationAsync(admin, "Head Office", "Site", root.Id, Token);
        var oldRoom = await DirectoryClient.CreateLocationAsync(admin, "Store Room", "Room", site.Id, Token);
        var newRoom = await DirectoryClient.CreateLocationAsync(admin, "Server Room", "Room", site.Id, Token);

        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var asset = await AssetsClient.CreateDetailedAsync(
            tech, new { assetTag = "LAP-2460", assetTypeId = typeId, locationId = oldRoom.Id }, Token);

        (await DirectoryClient.SendAsync(admin, HttpMethod.Delete, $"/api/v1/locations/{oldRoom.Id}", null, Token))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        await MoveAssetAsync(asset.Id, newRoom.Id);

        (await LocationUsageAsync(admin, oldRoom.Id)).CanDelete.ShouldBeTrue();
        (await DirectoryClient.SendAsync(admin, HttpMethod.Delete, $"/api/v1/locations/{oldRoom.Id}", null, Token))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// A soft-deleted asset cannot orphan anything, because nothing renders it. The
    /// global query filter is what makes that true without the counter saying so.
    /// </summary>
    [Fact]
    public async Task A_soft_deleted_asset_does_not_hold_a_room()
    {
        using var admin = await SignedInAsync("admin");
        using var tech = await SignedInAsync("tech");
        var room = await RoomAsync(admin);
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        var asset = await AssetsClient.CreateDetailedAsync(
            tech, new { assetTag = "LAP-2470", assetTypeId = typeId, locationId = room.Id }, Token);

        Count((await LocationUsageAsync(admin, room.Id)).References, "assets").ShouldBe(1);

        await SoftDeleteAssetAsync(asset.Id);

        var usage = await LocationUsageAsync(admin, room.Id);
        Count(usage.References, "assets").ShouldBe(0);
        usage.CanDelete.ShouldBeTrue();
    }

    /// <summary>
    /// A department is retired, never deleted (WP-0.6), so its usage read is a report
    /// rather than a gate — and retiring one with three hundred tickets against it is
    /// exactly what keeps those tickets resolving.
    /// </summary>
    [Fact]
    public async Task A_department_in_heavy_use_still_retires()
    {
        using var admin = await SignedInAsync("admin");
        using var tech = await SignedInAsync("tech");

        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        await TicketClient.CreateAsync(tech, reference, departmentId, "Laptop will not charge", Token);

        (await DepartmentUsageAsync(admin, departmentId)).TotalReferences.ShouldBe(1);

        var retire = await DirectoryClient.SendAsync(
            admin, HttpMethod.Post, $"/api/v1/departments/{departmentId}/deactivate", null, Token);
        retire.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterwards = await DepartmentUsageAsync(admin, departmentId);
        afterwards.IsActive.ShouldBeFalse();
        Count(afterwards.References, "tickets").ShouldBe(1);
    }

    [Fact]
    public async Task Usage_for_something_that_does_not_exist_is_a_404()
    {
        using var admin = await SignedInAsync("admin");

        var location = await admin.GetAsync(
            new Uri($"/api/v1/locations/{Guid.CreateVersion7()}/usage", UriKind.Relative), Token);
        location.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await DirectoryClient.ReadAsync<ProblemDto>(location, Token)).Code.ShouldBe("directory.location_not_found");

        var department = await admin.GetAsync(
            new Uri($"/api/v1/departments/{Guid.CreateVersion7()}/usage", UriKind.Relative), Token);
        department.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await DirectoryClient.ReadAsync<ProblemDto>(department, Token)).Code.ShouldBe("directory.department_not_found");
    }

    /// <summary>
    /// The usage reads are Admin, unlike the rest of the directory reads. They report how
    /// much equipment is in a room and how many people sit in it, which is inventory and
    /// staffing detail rather than the room's name.
    /// </summary>
    [Theory]
    [InlineData("tech")]
    [InlineData("user")]
    public async Task A_non_admin_may_read_a_location_but_not_its_usage(string userName)
    {
        using var admin = await SignedInAsync("admin");
        var room = await RoomAsync(admin);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        using var caller = await SignedInAsync(userName);

        (await caller.GetAsync(new Uri($"/api/v1/locations/{room.Id}", UriKind.Relative), Token))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await caller.GetAsync(new Uri($"/api/v1/locations/{room.Id}/usage", UriKind.Relative), Token))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await caller.GetAsync(new Uri($"/api/v1/departments/{departmentId}/usage", UriKind.Relative), Token))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static int Count(IReadOnlyList<UsageCountDto> references, string entityName) =>
        references.Single(reference => reference.EntityName == entityName).Count;

    private async Task<HttpClient> SignedInAsync(string userName)
    {
        var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>Builds a tree and hands back its one room, which is what most of these tests need.</summary>
    private static async Task<LocationDto> RoomAsync(HttpClient admin)
    {
        var root = await DirectoryClient.CreateLocationAsync(admin, "Northvale Utilities", "Organization", null, Token);
        var site = await DirectoryClient.CreateLocationAsync(admin, "Head Office", "Site", root.Id, Token);
        return await DirectoryClient.CreateLocationAsync(admin, "Server Room G-04", "Room", site.Id, Token);
    }

    private static async Task<LocationUsageDto> LocationUsageAsync(HttpClient admin, Guid locationId)
    {
        var response = await admin.GetAsync(
            new Uri($"/api/v1/locations/{locationId}/usage", UriKind.Relative), Token);
        response.EnsureSuccessStatusCode();
        return await DirectoryClient.ReadAsync<LocationUsageDto>(response, Token);
    }

    private static async Task<DepartmentUsageDto> DepartmentUsageAsync(HttpClient admin, Guid departmentId)
    {
        var response = await admin.GetAsync(
            new Uri($"/api/v1/departments/{departmentId}/usage", UriKind.Relative), Token);
        response.EnsureSuccessStatusCode();
        return await DirectoryClient.ReadAsync<DepartmentUsageDto>(response, Token);
    }

    /// <summary>
    /// Moves an asset between rooms directly in the table.
    /// </summary>
    /// <remarks>
    /// No asset edit endpoint exists — STATUS.md records it against Phase 2 as unowned —
    /// and inventing one here would be building WP-2.6's write path inside a Directory
    /// test. What is being asserted is Directory's behaviour when the reference goes away,
    /// so how the reference goes away is not the subject.
    /// </remarks>
    private static Task MoveAssetAsync(Guid assetId, Guid locationId) =>
        ExecuteAsync("UPDATE assets.assets SET location_id = @value WHERE id = @id", assetId, locationId);

    /// <summary>Soft-deletes an asset in the table, for the same reason as <see cref="MoveAssetAsync"/>.</summary>
    private static Task SoftDeleteAssetAsync(Guid assetId) =>
        ExecuteAsync("UPDATE assets.assets SET deleted_at = now() WHERE id = @id", assetId, value: null);

    private static async Task ExecuteAsync(string sql, Guid assetId, Guid? value)
    {
        await using var connection = new Npgsql.NpgsqlConnection(
            Itms.TestSupport.SharedPostgres.Instance.ConnectionStringFor(IdentityWebFixture.DatabaseName));
        await connection.OpenAsync(Token);

        await using var command = connection.CreateCommand();
        // The statement is a literal chosen by the test; only the values are parameters.
        command.CommandText = sql;
        command.Parameters.AddWithValue("id", assetId);
        if (value is { } locationId)
        {
            command.Parameters.AddWithValue("value", locationId);
        }

        await command.ExecuteNonQueryAsync(Token);
    }
}

/// <summary>One module's reference count, as the suite reads it off the wire.</summary>
/// <param name="EntityName">What was counted.</param>
/// <param name="Count">How many of them.</param>
public sealed record UsageCountDto(string EntityName, int Count);

/// <summary>A location's usage report, as the suite reads it off the wire.</summary>
/// <param name="LocationId">The location reported on.</param>
/// <param name="Name">Its own name.</param>
/// <param name="Path">Its full display path.</param>
/// <param name="ChildCount">How many locations sit directly beneath it.</param>
/// <param name="References">The per-module counts.</param>
/// <param name="TotalReferences">Their sum.</param>
/// <param name="CanDelete">Whether a delete would be accepted.</param>
public sealed record LocationUsageDto(
    Guid LocationId,
    string Name,
    string Path,
    int ChildCount,
    IReadOnlyList<UsageCountDto> References,
    int TotalReferences,
    bool CanDelete);

/// <summary>A department's usage report, as the suite reads it off the wire.</summary>
/// <param name="DepartmentId">The department reported on.</param>
/// <param name="Name">Its display name.</param>
/// <param name="IsActive">False once retired.</param>
/// <param name="References">The per-module counts.</param>
/// <param name="TotalReferences">Their sum.</param>
public sealed record DepartmentUsageDto(
    Guid DepartmentId,
    string Name,
    bool IsActive,
    IReadOnlyList<UsageCountDto> References,
    int TotalReferences);
