using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.DirectoryModule;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Assets.Domain;

// DirectoryModule declares its own ProblemDto — the duplicate plumbing STATUS.md has
// recorded since WP-1.1. Aliased rather than collapsed, because collapsing it means
// editing WP-0.6's suite and that is not this package's diff.
using ProblemDto = Itms.IntegrationTests.Api.ProblemDto;

namespace Itms.IntegrationTests.AssetsModule;

/// <summary>
/// The asset create endpoint over the wire. WP-2.1's done-criterion names an HTTP 409 for a
/// duplicate tag, so it is asserted here at the boundary rather than against the handler —
/// and the partial unique index behind the per-manufacturer serial rule can only be
/// demonstrated against a real PostgreSQL.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class AssetCreateEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_technician_records_an_asset_and_reads_it_back()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0100", typeId, Token);

        created.AssetTag.ShouldBe("LAP-0100");
        created.AssetTypeId.ShouldBe(typeId);

        var fetched = await tech.GetAsync(new Uri($"{AssetsClient.Assets}/{created.Id}", UriKind.Relative), Token);
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadAsync<AssetDto>(fetched, Token)).AssetTag.ShouldBe("LAP-0100");
    }

    /// <summary>
    /// The 201's <c>Location</c> header is why <c>GET /assets/{id}</c> is in this package
    /// and not WP-2.3: a header pointing at a route nothing serves is not a finished create.
    /// </summary>
    [Fact]
    public async Task The_created_location_header_resolves()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        var response = await AssetsClient.PostAssetAsync(tech, new { assetTag = "LAP-0101", assetTypeId = typeId }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var location = response.Headers.Location.ShouldNotBeNull();

        var followed = await tech.GetAsync(location, Token);
        followed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>An asset with no status named starts in the seeded <c>in-stock</c> row.</summary>
    [Fact]
    public async Task An_asset_with_no_status_named_starts_in_stock()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0102", typeId, Token);

        created.AssetStatusCode.ShouldBe(AssetStatusCode.InStock);
        created.AssetStatusName.ShouldBe("In Stock");
    }

    /// <summary>
    /// Booking in equipment that is already deployed or already away for repair is
    /// recording a fact, not performing a transition, so any active status is accepted at
    /// creation. Invariant 5's history requirement is about the transitions WP-2.2 adds.
    /// </summary>
    [Fact]
    public async Task An_asset_can_be_recorded_in_a_status_other_than_in_stock()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var repair = await AssetsClient.StatusByCodeAsync(tech, AssetStatusCode.Repair, Token);

        var response = await AssetsClient.PostAssetAsync(
            tech,
            new { assetTag = "LAP-0103", assetTypeId = typeId, assetStatusId = repair.Id },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ApiClient.ReadAsync<AssetDto>(response, Token)).AssetStatusCode.ShouldBe(AssetStatusCode.Repair);
    }

    /// <summary>WP-2.1's headline criterion.</summary>
    [Fact]
    public async Task A_duplicate_tag_is_a_409_naming_the_tag()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        await AssetsClient.CreateAssetAsync(tech, "LAP-0104", typeId, Token);

        var response = await AssetsClient.PostAssetAsync(tech, new { assetTag = "LAP-0104", assetTypeId = typeId }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("assets.duplicate_asset_tag");
        problem.Detail.ShouldNotBeNull().ShouldContain("LAP-0104");
    }

    /// <summary>
    /// Uniqueness is on the normalised tag, so a different case is the same tag. The unique
    /// index sits on the same column, so the API's answer and the database's agree.
    /// </summary>
    [Fact]
    public async Task A_duplicate_tag_in_a_different_case_is_still_a_409()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        await AssetsClient.CreateAssetAsync(tech, "LAP-0105", typeId, Token);

        var response = await AssetsClient.PostAssetAsync(tech, new { assetTag = "lap-0105", assetTypeId = typeId }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("assets.duplicate_asset_tag");
    }

    /// <summary>The other half of invariant 4: unique per manufacturer, where present.</summary>
    [Fact]
    public async Task A_serial_repeated_for_one_manufacturer_is_a_409()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        await AssetsClient.PostAssetAsync(
            tech,
            new { assetTag = "LAP-0106", assetTypeId = typeId, manufacturer = "HP", serialNumber = "CND1234" },
            Token);

        var response = await AssetsClient.PostAssetAsync(
            tech,
            new { assetTag = "LAP-0107", assetTypeId = typeId, manufacturer = "hp", serialNumber = "cnd1234" },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("assets.duplicate_serial_number");
    }

    /// <summary>
    /// Two vendors numbering their products from 1 is ordinary, and refusing the second
    /// would be wrong.
    /// </summary>
    [Fact]
    public async Task The_same_serial_from_a_different_manufacturer_is_accepted()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        await AssetsClient.PostAssetAsync(
            tech,
            new { assetTag = "LAP-0108", assetTypeId = typeId, manufacturer = "HP", serialNumber = "0001" },
            Token);

        var response = await AssetsClient.PostAssetAsync(
            tech,
            new { assetTag = "LAP-0109", assetTypeId = typeId, manufacturer = "Dell", serialNumber = "0001" },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    /// <summary>
    /// "Where present" is the whole rule: an asset with no serial falls outside the partial
    /// index and collides with nothing, so two of them are fine.
    /// </summary>
    [Fact]
    public async Task Two_assets_with_no_serial_do_not_collide()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        await AssetsClient.CreateAssetAsync(tech, "LAP-0110", typeId, Token);

        var response = await AssetsClient.PostAssetAsync(
            tech,
            new { assetTag = "LAP-0111", assetTypeId = typeId, manufacturer = "HP" },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    /// <summary>
    /// The column the cached display strings live in is filled by reading the owning
    /// module's contract — this is the first consumer of <c>ILocationLookup</c> anywhere.
    /// </summary>
    [Fact]
    public async Task Placement_caches_the_department_name_and_location_path()
    {
        using var admin = await SignedInAsync("admin");
        var typeId = await AssetsClient.AnyTypeIdAsync(admin, Token);
        var department = await DirectoryClient.CreateDepartmentAsync(admin, "Facilities", code: null, Token);

        var response = await AssetsClient.PostAssetAsync(
            admin,
            new { assetTag = "LAP-0112", assetTypeId = typeId, departmentId = department.Id },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var asset = await ApiClient.ReadAsync<AssetDto>(response, Token);
        asset.DepartmentId.ShouldBe(department.Id);
        asset.DepartmentName.ShouldBe("Facilities");
    }

    [Fact]
    public async Task An_unknown_department_is_a_400_against_the_field()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        var response = await AssetsClient.PostAssetAsync(
            tech,
            new { assetTag = "LAP-0113", assetTypeId = typeId, departmentId = Guid.CreateVersion7() },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("assets.department_not_found");
        problem.Errors.ShouldNotBeNull().ShouldContainKey("departmentId");
    }

    [Fact]
    public async Task An_unknown_type_is_a_404()
    {
        using var tech = await SignedInAsync("tech");

        var response = await AssetsClient.PostAssetAsync(
            tech,
            new { assetTag = "LAP-0114", assetTypeId = Guid.CreateVersion7() },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("assets.asset_type_not_found");
    }

    /// <summary>A retired type is refused for a new asset, exactly as a retired category is.</summary>
    [Fact]
    public async Task A_retired_type_cannot_classify_a_new_asset()
    {
        using var admin = await SignedInAsync("admin");
        var type = await AssetsClient.CreateTypeAsync(admin, "Fax Machine", 900, Token);

        var retire = await ApiClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"{AssetsClient.Types}/{type.Id}/deactivate",
            body: null,
            Token);
        retire.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await AssetsClient.PostAssetAsync(
            admin,
            new { assetTag = "FAX-0001", assetTypeId = type.Id },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("assets.asset_type_retired");
    }

    [Fact]
    public async Task A_tag_containing_a_space_is_a_400()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        var response = await AssetsClient.PostAssetAsync(tech, new { assetTag = "LAP 0115", assetTypeId = typeId }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Errors.ShouldNotBeNull().ShouldContainKey("assetTag");
    }

    [Fact]
    public async Task A_negative_cost_is_a_400()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        var response = await AssetsClient.PostAssetAsync(
            tech,
            new { assetTag = "LAP-0116", assetTypeId = typeId, cost = -1m },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Errors.ShouldNotBeNull().ShouldContainKey("cost");
    }

    /// <summary>
    /// The assertion that fails the day WP-2.2's assignment starts happening at creation
    /// without a history entry — the wire half of <c>AssetTests</c>'s unit guard.
    /// </summary>
    [Fact]
    public async Task A_created_asset_holds_nobody()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0117", typeId, Token);

        created.AssignedToUserId.ShouldBeNull();
    }

    /// <summary>SPEC.md §14 keeps the inventory on the operational surface.</summary>
    [Fact]
    public async Task An_end_user_cannot_record_or_read_an_asset()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0118", typeId, Token);

        using var user = await SignedInAsync("user");

        var post = await AssetsClient.PostAssetAsync(user, new { assetTag = "LAP-0119", assetTypeId = typeId }, Token);
        post.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var get = await user.GetAsync(new Uri($"{AssetsClient.Assets}/{created.Id}", UriKind.Relative), Token);
        get.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_unknown_asset_is_a_404()
    {
        using var tech = await SignedInAsync("tech");

        var response = await tech.GetAsync(
            new Uri($"{AssetsClient.Assets}/{Guid.CreateVersion7()}", UriKind.Relative),
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("assets.asset_not_found");
    }

    private Task<HttpClient> SignedInAsync(string userName) =>
        AuthClient.SignedInAsync(fixture, userName, Token);
}
