using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Assets.Domain;

// DirectoryModule declares its own ProblemDto — see AssetCreateEndpointTests on why this
// is aliased rather than collapsed.
using ProblemDto = Itms.IntegrationTests.Api.ProblemDto;

namespace Itms.IntegrationTests.AssetsModule;

/// <summary>
/// The asset-type and asset-status endpoints over the wire: the seed, uniqueness, the
/// rename existing assets follow, the immutable status code, retirement instead of
/// deletion, and the role boundary SPEC.md §13 puts around administration.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class AssetReferenceDataEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// The seeder ran. This is also what proves the fixture's <c>ResetAsync</c> puts the
    /// reference data back after a Respawn truncate — without that, every other test in this
    /// namespace would fail for an unrelated reason.
    /// </summary>
    [Fact]
    public async Task The_twelve_types_and_six_statuses_are_seeded()
    {
        using var tech = await SignedInAsync("tech");

        var types = await ApiClient.ListAsync<AssetTypeDto>(tech, AssetsClient.Types, Token);
        var statuses = await ApiClient.ListAsync<AssetStatusDto>(tech, AssetsClient.Statuses, Token);

        types.Total.ShouldBe(12);
        types.Items.Select(type => type.Name).ShouldContain("Laptop");
        statuses.Total.ShouldBe(6);
        statuses.Items.Select(status => status.Code).ShouldBe(
            ["in-stock", "deployed", "repair", "retired", "lost", "disposed"]);
    }

    [Fact]
    public async Task An_admin_creates_a_type_and_can_read_it_back()
    {
        using var admin = await SignedInAsync("admin");

        var created = await AssetsClient.CreateTypeAsync(admin, "Docking Station", 130, Token);

        created.Name.ShouldBe("Docking Station");
        created.IsActive.ShouldBeTrue();

        var fetched = await admin.GetAsync(new Uri($"{AssetsClient.Types}/{created.Id}", UriKind.Relative), Token);
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadAsync<AssetTypeDto>(fetched, Token)).Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task A_duplicate_type_name_is_a_409_whatever_its_case()
    {
        using var admin = await SignedInAsync("admin");

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Post,
            AssetsClient.Types,
            new { name = "laptop", description = (string?)null, sortOrder = 500 },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("assets.duplicate_asset_type_name");
    }

    [Fact]
    public async Task A_duplicate_status_code_is_a_409()
    {
        using var admin = await SignedInAsync("admin");

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Post,
            AssetsClient.Statuses,
            new { code = "deployed", name = "Out On Loan", description = (string?)null, sortOrder = 500 },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("assets.duplicate_asset_status_code");
    }

    [Fact]
    public async Task A_malformed_status_code_is_a_400_against_the_field()
    {
        using var admin = await SignedInAsync("admin");

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Post,
            AssetsClient.Statuses,
            new { code = "Not A Code", name = "Whatever", description = (string?)null, sortOrder = 500 },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Errors.ShouldNotBeNull().ShouldContainKey("code");
    }

    /// <summary>
    /// The rename reaches the asset because the asset stores the id and nothing else — the
    /// whole reason the type's name is not copied onto the row.
    /// </summary>
    [Fact]
    public async Task Renaming_a_type_is_visible_on_an_asset_already_classified_under_it()
    {
        using var admin = await SignedInAsync("admin");
        var type = await AssetsClient.CreateTypeAsync(admin, "Handheld", 140, Token);
        var asset = await AssetsClient.CreateAssetAsync(admin, "HH-0001", type.Id, Token);

        var renamed = await ApiClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{AssetsClient.Types}/{type.Id}",
            new { name = "Rugged Handheld", description = (string?)null, sortOrder = 140 },
            Token);
        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);

        var fetched = await admin.GetAsync(new Uri($"{AssetsClient.Assets}/{asset.Id}", UriKind.Relative), Token);
        (await ApiClient.ReadAsync<AssetDto>(fetched, Token)).AssetTypeName.ShouldBe("Rugged Handheld");
    }

    /// <summary>
    /// A rename must not move the code, because WP-2.2's lifecycle methods key off it.
    /// </summary>
    [Fact]
    public async Task Renaming_a_status_leaves_its_code_alone_over_the_wire()
    {
        using var admin = await SignedInAsync("admin");
        var repair = await AssetsClient.StatusByCodeAsync(admin, AssetStatusCode.Repair, Token);

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{AssetsClient.Statuses}/{repair.Id}",
            new { name = "Being Fixed", description = (string?)null, sortOrder = repair.SortOrder },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ApiClient.ReadAsync<AssetStatusDto>(response, Token);
        updated.Name.ShouldBe("Being Fixed");
        updated.Code.ShouldBe(AssetStatusCode.Repair);
    }

    /// <summary>
    /// Retirement is the removal path, and a retired row keeps resolving for the assets
    /// already pointing at it.
    /// </summary>
    [Fact]
    public async Task A_retired_type_still_resolves_on_an_existing_asset()
    {
        using var admin = await SignedInAsync("admin");
        var type = await AssetsClient.CreateTypeAsync(admin, "Plotter", 150, Token);
        var asset = await AssetsClient.CreateAssetAsync(admin, "PLT-0001", type.Id, Token);

        var retired = await ApiClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"{AssetsClient.Types}/{type.Id}/deactivate",
            body: null,
            Token);
        retired.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var fetched = await admin.GetAsync(new Uri($"{AssetsClient.Assets}/{asset.Id}", UriKind.Relative), Token);
        (await ApiClient.ReadAsync<AssetDto>(fetched, Token)).AssetTypeName.ShouldBe("Plotter");

        // And it is gone from the picker, which is what retirement is for.
        var active = await ApiClient.ListAsync<AssetTypeDto>(admin, AssetsClient.Types, Token);
        active.Items.Select(candidate => candidate.Id).ShouldNotContain(type.Id);

        var all = await ApiClient.ListAsync<AssetTypeDto>(admin, $"{AssetsClient.Types}?includeInactive=true", Token);
        all.Items.Select(candidate => candidate.Id).ShouldContain(type.Id);
    }

    /// <summary>There is no DELETE on either route — retirement is the only removal path.</summary>
    [Theory]
    [InlineData(AssetsClient.Types)]
    [InlineData(AssetsClient.Statuses)]
    public async Task There_is_no_delete_route(string route)
    {
        using var admin = await SignedInAsync("admin");
        var page = await ApiClient.ListAsync<AssetTypeDto>(admin, route, Token);

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Delete,
            $"{route}/{page.Items[0].Id}",
            body: null,
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    /// <summary>
    /// SPEC.md §13 puts asset types and statuses under administration. A technician runs the
    /// operational surface and does not configure it.
    /// </summary>
    [Theory]
    [InlineData(AssetsClient.Types)]
    [InlineData(AssetsClient.Statuses)]
    public async Task A_technician_can_read_the_reference_data_but_not_write_it(string route)
    {
        using var tech = await SignedInAsync("tech");

        var read = await tech.GetAsync(new Uri(route, UriKind.Relative), Token);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);

        var write = await ApiClient.SendAsync(
            tech,
            HttpMethod.Post,
            route,
            new { code = "whatever", name = "Whatever", description = (string?)null, sortOrder = 999 },
            Token);

        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// An end user picks nothing from these lists, but reading them is harmless and the
    /// reads sit on the same Authenticated policy the ticket reference data does.
    /// </summary>
    [Fact]
    public async Task An_end_user_can_read_the_reference_data()
    {
        using var user = await SignedInAsync("user");

        var response = await user.GetAsync(new Uri(AssetsClient.Types, UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_anonymous_caller_gets_401_not_a_redirect()
    {
        using var anonymous = fixture.CreateClient();

        var response = await anonymous.GetAsync(new Uri(AssetsClient.Types, UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private Task<HttpClient> SignedInAsync(string userName) =>
        AuthClient.SignedInAsync(fixture, userName, Token);
}
