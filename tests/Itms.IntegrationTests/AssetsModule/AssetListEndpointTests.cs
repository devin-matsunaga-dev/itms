using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.DirectoryModule;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Assets.Domain;

// DirectoryModule declares its own ProblemDto — the duplicate plumbing STATUS.md has
// recorded since WP-1.1. Aliased rather than collapsed, following the two asset suites
// WP-2.1 wrote, because collapsing it means editing WP-0.6's suite.
using ProblemDto = Itms.IntegrationTests.Api.ProblemDto;

namespace Itms.IntegrationTests.AssetsModule;

/// <summary>
/// The asset list over the wire. WP-2.3's whole surface — filtering by type, status,
/// department, location, holder and warranty window, search, paging and sorting — and its
/// done-criterion, which names the warranty filter.
/// </summary>
/// <remarks>
/// <para>
/// Asserted here rather than against the handler because every one of these is a question
/// about SQL: an <c>ILIKE</c> escape, a null ordering, a page boundary, and a warranty
/// comparison between a <see cref="DateOnly"/> column and a server-side date. None of them
/// can be proved in memory, and the ones that look most obvious — nulls sorting last,
/// three-valued logic dropping a null from a range — are exactly the ones a provider decides
/// rather than the code.
/// </para>
/// <para>
/// <b>Most assertions compare tag sequences.</b> The interesting fact about a list query is
/// which assets came back and in what order; comparing whole rows says the same thing at ten
/// times the length and fails less legibly.
/// </para>
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class AssetListEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task The_default_list_is_every_asset_by_tag_ascending()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        // Recorded out of order, and in mixed case, so neither insertion order nor a
        // case-sensitive collation could produce the expected answer by accident.
        await AssetsClient.CreateAssetAsync(tech, "SRV-0002", typeId, Token);
        await AssetsClient.CreateAssetAsync(tech, "lap-0001", typeId, Token);
        await AssetsClient.CreateAssetAsync(tech, "LAP-0003", typeId, Token);

        var tags = await AssetsClient.TagsAsync(tech, string.Empty, Token);

        tags.ShouldBe(["lap-0001", "LAP-0003", "SRV-0002"]);
    }

    /// <summary>
    /// The displayed case is the operator's (WP-2.1) but the ordering is not, or an estate
    /// tagged inconsistently would interleave into nonsense.
    /// </summary>
    [Fact]
    public async Task The_tag_ordering_ignores_case()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        await AssetsClient.CreateAssetAsync(tech, "aaa-2", typeId, Token);
        await AssetsClient.CreateAssetAsync(tech, "AAA-1", typeId, Token);
        await AssetsClient.CreateAssetAsync(tech, "AAA-3", typeId, Token);

        (await AssetsClient.TagsAsync(tech, string.Empty, Token)).ShouldBe(["AAA-1", "aaa-2", "AAA-3"]);
    }

    [Fact]
    public async Task An_empty_estate_is_an_empty_page_and_not_a_404()
    {
        using var tech = await SignedInAsync("tech");

        var page = await AssetsClient.ListAssetsAsync(tech, string.Empty, Token);

        page.Items.ShouldBeEmpty();
        page.Total.ShouldBe(0);
        page.Page.ShouldBe(1);
    }

    /// <summary>
    /// SPEC.md §14 puts the inventory on the operational surface. An end user has no
    /// requester-scoped view of it, so the answer is 403 rather than a narrowed list.
    /// </summary>
    [Fact]
    public async Task An_end_user_may_not_enumerate_the_estate()
    {
        using var tech = await SignedInAsync("tech");
        using var user = await SignedInAsync("user");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        await AssetsClient.CreateAssetAsync(tech, "LAP-0400", typeId, Token);

        var response = await user.GetAsync(new Uri(AssetsClient.Assets, UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_anonymous_caller_is_refused()
    {
        using var anonymous = fixture.CreateClient();

        var response = await anonymous.GetAsync(new Uri(AssetsClient.Assets, UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_list_filters_by_type()
    {
        using var tech = await SignedInAsync("tech");
        using var admin = await SignedInAsync("admin");
        var laptops = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var printers = await AssetsClient.CreateTypeAsync(admin, "Field Printer", 900, Token);

        await AssetsClient.CreateAssetAsync(tech, "LAP-0410", laptops, Token);
        await AssetsClient.CreateAssetAsync(tech, "PRN-0411", printers.Id, Token);

        (await AssetsClient.TagsAsync(tech, $"assetTypeId={printers.Id}", Token)).ShouldBe(["PRN-0411"]);
    }

    /// <summary>
    /// Repeatable, because "in service" is not a status but two of them — the call WP-1.5
    /// made for the ticket queue's status filter.
    /// </summary>
    [Fact]
    public async Task The_status_filter_accepts_several_statuses_at_once()
    {
        using var tech = await SignedInAsync("tech");
        await EstateAsync(tech);

        var inStock = await AssetsClient.StatusByCodeAsync(tech, AssetStatusCode.InStock, Token);
        var repair = await AssetsClient.StatusByCodeAsync(tech, AssetStatusCode.Repair, Token);

        var tags = await AssetsClient.TagsAsync(
            tech,
            $"assetStatusId={inStock.Id}&assetStatusId={repair.Id}",
            Token);

        tags.ShouldBe(["STOCK-1", "STOCK-2", "WORKSHOP-1"]);
    }

    /// <summary>A repeated value reaches the database as a longer IN list saying the same thing.</summary>
    [Fact]
    public async Task A_repeated_status_is_not_a_wider_filter()
    {
        using var tech = await SignedInAsync("tech");
        await EstateAsync(tech);

        var repair = await AssetsClient.StatusByCodeAsync(tech, AssetStatusCode.Repair, Token);

        var tags = await AssetsClient.TagsAsync(
            tech,
            $"assetStatusId={repair.Id}&assetStatusId={repair.Id}",
            Token);

        tags.ShouldBe(["WORKSHOP-1"]);
    }

    /// <summary>
    /// The second way to name a status. An id belongs to one database; a code is the same in
    /// every deployment, which is what makes a seeded dashboard link survive a restore.
    /// </summary>
    [Fact]
    public async Task The_list_filters_by_status_code()
    {
        using var tech = await SignedInAsync("tech");
        await EstateAsync(tech);

        var tags = await AssetsClient.TagsAsync(tech, $"statusCode={AssetStatusCode.Deployed}", Token);

        tags.ShouldBe(["DESK-1"]);
    }

    [Fact]
    public async Task The_status_code_filter_is_repeatable_and_case_insensitive()
    {
        using var tech = await SignedInAsync("tech");
        await EstateAsync(tech);

        var tags = await AssetsClient.TagsAsync(tech, "statusCode=DEPLOYED&statusCode=Repair", Token);

        tags.ShouldBe(["DESK-1", "WORKSHOP-1"]);
    }

    /// <summary>An unrecognised code is a filter matching nothing, not an error.</summary>
    [Fact]
    public async Task An_unknown_status_code_matches_nothing()
    {
        using var tech = await SignedInAsync("tech");
        await EstateAsync(tech);

        var page = await AssetsClient.ListAssetsAsync(tech, "statusCode=not-a-status", Token);

        page.Items.ShouldBeEmpty();
        page.Total.ShouldBe(0);
    }

    /// <summary>
    /// The two ways of naming a status narrow together, like every other filter here — the
    /// warranty pair below is the deliberate exception.
    /// </summary>
    [Fact]
    public async Task An_id_and_a_code_naming_different_statuses_intersect_to_nothing()
    {
        using var tech = await SignedInAsync("tech");
        await EstateAsync(tech);

        var inStock = await AssetsClient.StatusByCodeAsync(tech, AssetStatusCode.InStock, Token);

        var page = await AssetsClient.ListAssetsAsync(
            tech,
            $"assetStatusId={inStock.Id}&statusCode={AssetStatusCode.Deployed}",
            Token);

        page.Total.ShouldBe(0);
    }

    [Fact]
    public async Task The_list_filters_by_department_and_by_location()
    {
        using var tech = await SignedInAsync("tech");
        using var admin = await SignedInAsync("admin");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        var finance = await DirectoryClient.CreateDepartmentAsync(admin, "Finance", "FIN", Token);
        var org = await DirectoryClient.CreateLocationAsync(admin, "Riverside Group", "Organization", null, Token);
        var site = await DirectoryClient.CreateLocationAsync(admin, "Riverside", "Site", org.Id, Token);

        await AssetsClient.CreateDetailedAsync(
            tech,
            new { assetTag = "FIN-0001", assetTypeId = typeId, departmentId = finance.Id, locationId = site.Id },
            Token);
        await AssetsClient.CreateAssetAsync(tech, "UNFILED-0001", typeId, Token);

        (await AssetsClient.TagsAsync(tech, $"departmentId={finance.Id}", Token)).ShouldBe(["FIN-0001"]);
        (await AssetsClient.TagsAsync(tech, $"locationId={site.Id}", Token)).ShouldBe(["FIN-0001"]);
    }

    [Fact]
    public async Task The_list_filters_by_holder_and_by_having_none()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var alice = await UserIdAsync("user");

        var issued = await AssetsClient.CreateAssetAsync(tech, "HELD-0001", typeId, Token);
        (await AssetsClient.AssignAsync(tech, issued.Id, alice, Token)).EnsureSuccessStatusCode();
        await AssetsClient.CreateAssetAsync(tech, "SPARE-0001", typeId, Token);

        (await AssetsClient.TagsAsync(tech, $"assignedToUserId={alice}", Token)).ShouldBe(["HELD-0001"]);
        (await AssetsClient.TagsAsync(tech, "unassigned=true", Token)).ShouldBe(["SPARE-0001"]);
    }

    /// <summary>
    /// Asking for both is a contradiction, and answering the narrower of the two is the safe
    /// reading — the call WP-1.5 made for an unassigned ticket.
    /// </summary>
    [Fact]
    public async Task Unassigned_wins_over_a_named_holder()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var alice = await UserIdAsync("user");

        var issued = await AssetsClient.CreateAssetAsync(tech, "HELD-0002", typeId, Token);
        (await AssetsClient.AssignAsync(tech, issued.Id, alice, Token)).EnsureSuccessStatusCode();
        await AssetsClient.CreateAssetAsync(tech, "SPARE-0002", typeId, Token);

        var tags = await AssetsClient.TagsAsync(tech, $"unassigned=true&assignedToUserId={alice}", Token);

        tags.ShouldBe(["SPARE-0002"]);
    }

    [Fact]
    public async Task The_search_matches_tag_serial_name_manufacturer_and_model()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        await AssetsClient.CreateDetailedAsync(
            tech,
            new { assetTag = "ZEBRA-0001", assetTypeId = typeId },
            Token);
        await AssetsClient.CreateDetailedAsync(
            tech,
            new { assetTag = "AAA-0002", assetTypeId = typeId, serialNumber = "SN-ZEBRA-9" },
            Token);
        await AssetsClient.CreateDetailedAsync(
            tech,
            new { assetTag = "AAA-0003", assetTypeId = typeId, name = "Zebra label printer" },
            Token);
        await AssetsClient.CreateDetailedAsync(
            tech,
            new { assetTag = "AAA-0004", assetTypeId = typeId, manufacturer = "Zebra" },
            Token);
        await AssetsClient.CreateDetailedAsync(
            tech,
            new { assetTag = "AAA-0005", assetTypeId = typeId, model = "ZebraJet 40" },
            Token);
        await AssetsClient.CreateAssetAsync(tech, "AAA-0006", typeId, Token);

        var tags = await AssetsClient.TagsAsync(tech, "search=zebra", Token);

        tags.ShouldBe(["AAA-0002", "AAA-0003", "AAA-0004", "AAA-0005", "ZEBRA-0001"]);
    }

    /// <summary>
    /// The escaping is the shared kernel's. An unescaped <c>%</c> typed into the box would
    /// otherwise become a wildcard over the whole table — a silent failure, since the query
    /// still runs and still returns something.
    /// </summary>
    [Fact]
    public async Task A_wildcard_typed_into_the_search_box_is_a_literal()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        await AssetsClient.CreateAssetAsync(tech, "LIT-100%-OK", typeId, Token);
        await AssetsClient.CreateAssetAsync(tech, "LIT-200-OK", typeId, Token);

        // As a wildcard this would match both; as a literal it matches one.
        (await AssetsClient.TagsAsync(tech, "search=100%25-OK", Token)).ShouldBe(["LIT-100%-OK"]);

        // The underscore is the other wildcard, and the one nobody remembers.
        (await AssetsClient.TagsAsync(tech, "search=LIT_100", Token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_search_is_case_insensitive_and_matches_the_middle_of_a_value()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        await AssetsClient.CreateAssetAsync(tech, "LAP-0420", typeId, Token);

        (await AssetsClient.TagsAsync(tech, "search=p-04", Token)).ShouldBe(["LAP-0420"]);
    }

    // ---- The warranty window: WP-2.3's done-criterion ----

    /// <summary>
    /// The criterion: warranty-expiring-within-N-days is a first-class filter, inclusive at
    /// both ends, and it excludes the warranties that have already lapsed.
    /// </summary>
    [Fact]
    public async Task Warranty_expiring_within_n_days_is_inclusive_and_excludes_the_lapsed()
    {
        using var tech = await SignedInAsync("tech");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await WarrantyEstateAsync(tech, today);

        var tags = await AssetsClient.TagsAsync(tech, "warrantyExpiringInDays=30", Token);

        // TODAY and DAY-30 are the boundaries and both are in; LAPSED is before the window
        // and DAY-31 after it; NONE has no date to be expiring.
        tags.ShouldBe(["DAY-30", "DAY-7", "TODAY"]);
    }

    /// <summary>Zero days is the warranties running out today, not an empty window.</summary>
    [Fact]
    public async Task Warranty_expiring_within_zero_days_is_today()
    {
        using var tech = await SignedInAsync("tech");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await WarrantyEstateAsync(tech, today);

        (await AssetsClient.TagsAsync(tech, "warrantyExpiringInDays=0", Token)).ShouldBe(["TODAY"]);
    }

    [Fact]
    public async Task Warranty_expired_is_the_lapsed_ones_only()
    {
        using var tech = await SignedInAsync("tech");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await WarrantyEstateAsync(tech, today);

        (await AssetsClient.TagsAsync(tech, "warrantyExpired=true", Token)).ShouldBe(["LAPSED"]);
    }

    /// <summary>
    /// An asset with no warranty date recorded has not expired, so it is on the
    /// not-expired list. SQL's three-valued logic would drop it along with the lapsed ones
    /// if the handler did not say so explicitly.
    /// </summary>
    [Fact]
    public async Task An_asset_with_no_warranty_date_has_not_expired()
    {
        using var tech = await SignedInAsync("tech");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await WarrantyEstateAsync(tech, today);

        var tags = await AssetsClient.TagsAsync(tech, "warrantyExpired=false", Token);

        tags.ShouldContain("NONE");
        tags.ShouldNotContain("LAPSED");
    }

    /// <summary>
    /// The one pair on this query that widens rather than narrows. The two windows are
    /// disjoint by construction, so intersecting them could only ever return nothing — and
    /// "already lapsed, or lapsing within thirty days" is the list somebody chasing
    /// warranties actually wants.
    /// </summary>
    [Fact]
    public async Task Expired_and_expiring_together_are_the_union()
    {
        using var tech = await SignedInAsync("tech");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await WarrantyEstateAsync(tech, today);

        var tags = await AssetsClient.TagsAsync(tech, "warrantyExpiringInDays=30&warrantyExpired=true", Token);

        tags.ShouldBe(["DAY-30", "DAY-7", "LAPSED", "TODAY"]);
    }

    /// <summary>
    /// The other pairing narrows, and is a no-op here because everything inside the window
    /// is already unexpired. Asserted so the union above cannot be mistaken for both.
    /// </summary>
    [Fact]
    public async Task Expiring_with_expired_false_still_narrows()
    {
        using var tech = await SignedInAsync("tech");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await WarrantyEstateAsync(tech, today);

        var tags = await AssetsClient.TagsAsync(tech, "warrantyExpiringInDays=30&warrantyExpired=false", Token);

        tags.ShouldBe(["DAY-30", "DAY-7", "TODAY"]);
    }

    /// <summary>A negative window is a filter matching nothing, not a 400 and not a 500.</summary>
    [Fact]
    public async Task A_negative_warranty_window_matches_nothing()
    {
        using var tech = await SignedInAsync("tech");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await WarrantyEstateAsync(tech, today);

        var page = await AssetsClient.ListAssetsAsync(tech, "warrantyExpiringInDays=-5", Token);

        page.Total.ShouldBe(0);
    }

    /// <summary>
    /// The clamp, over the wire. <c>DateOnly.AddDays</c> throws once the result leaves the
    /// calendar, so without it an unbounded integer off the query string is a 500.
    /// </summary>
    [Fact]
    public async Task An_absurd_warranty_window_is_every_asset_with_a_date()
    {
        using var tech = await SignedInAsync("tech");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await WarrantyEstateAsync(tech, today);

        var tags = await AssetsClient.TagsAsync(
            tech,
            $"warrantyExpiringInDays={int.MaxValue}",
            Token);

        // Everything but LAPSED, which is before the window, and NONE, which has no date.
        tags.ShouldBe(["DAY-30", "DAY-31", "DAY-7", "TODAY"]);
    }

    /// <summary>
    /// The exact query SPEC.md §1's expiry tile issues — the criterion's "matches the
    /// dashboard tile" half. Soonest first, and the tile's figure is the envelope's total.
    /// </summary>
    [Fact]
    public async Task The_dashboard_tile_query_reads_soonest_first()
    {
        using var tech = await SignedInAsync("tech");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await WarrantyEstateAsync(tech, today);

        var page = await AssetsClient.ListAssetsAsync(
            tech,
            "warrantyExpiringInDays=30&sort=WarrantyExpiresAt&direction=Ascending",
            Token);

        page.Items.Select(asset => asset.AssetTag).ShouldBe(["TODAY", "DAY-7", "DAY-30"]);
        page.Total.ShouldBe(3);
    }

    // ---- Sorting ----

    [Fact]
    public async Task Warranty_sort_defaults_to_soonest_first_and_puts_the_undated_last()
    {
        using var tech = await SignedInAsync("tech");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await WarrantyEstateAsync(tech, today);

        var tags = await AssetsClient.TagsAsync(tech, "sort=WarrantyExpiresAt", Token);

        // No direction given, so ascending — the useful end of a warranty list is the front.
        // "No date recorded" is not "expiring imminently", so NONE sorts last.
        tags.ShouldBe(["LAPSED", "TODAY", "DAY-7", "DAY-30", "DAY-31", "NONE"]);
    }

    [Fact]
    public async Task Warranty_sort_descending_puts_the_undated_first()
    {
        using var tech = await SignedInAsync("tech");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await WarrantyEstateAsync(tech, today);

        var tags = await AssetsClient.TagsAsync(tech, "sort=WarrantyExpiresAt&direction=Descending", Token);

        tags.ShouldBe(["NONE", "DAY-31", "DAY-30", "DAY-7", "TODAY", "LAPSED"]);
    }

    /// <summary>
    /// Ordered by the status's own <c>SortOrder</c>, so one list cannot disagree with a
    /// picker about what order the statuses come in.
    /// </summary>
    [Fact]
    public async Task The_status_sort_follows_the_status_sort_order()
    {
        using var tech = await SignedInAsync("tech");
        await EstateAsync(tech);

        var statuses = await ApiClient.ListAsync<AssetStatusDto>(tech, AssetsClient.Statuses, Token);
        var order = statuses.Items.ToDictionary(status => status.Code, status => status.SortOrder);

        // Status is not one of the two sorts whose useful end is the front, so no direction
        // means descending — the rule the handler applies to every sort but AssetTag and
        // WarrantyExpiresAt. Both directions are asserted so the default is pinned rather
        // than inferred from whichever one happened to be written.
        var descending = await RanksAsync(tech, "sort=Status", order);
        descending.ShouldBe(descending.OrderByDescending(rank => rank).ToList());

        var ascending = await RanksAsync(tech, "sort=Status&direction=Ascending", order);
        ascending.ShouldBe(ascending.OrderBy(rank => rank).ToList());

        // Not vacuous: the estate spans four statuses, so the two orders really differ.
        ascending.Distinct().Count().ShouldBe(4);
        ascending.ShouldNotBe(descending);
    }

    /// <summary>The status sort orders of a query's rows, in the order it returned them.</summary>
    /// <param name="tech">A signed-in technician.</param>
    /// <param name="query">The query string, without its leading <c>?</c>.</param>
    /// <param name="order">Each status code's sort order.</param>
    /// <returns>The ranks, in page order.</returns>
    private static async Task<List<int>> RanksAsync(
        HttpClient tech,
        string query,
        Dictionary<string, int> order)
    {
        var page = await AssetsClient.ListAssetsAsync(tech, query, Token);
        return [.. page.Items.Select(asset => order[asset.AssetStatusCode])];
    }

    /// <summary>
    /// A queue defaults to newest first; a register does not. "What was added recently" is
    /// a real question, and it is asked for rather than assumed on the reader's behalf.
    /// </summary>
    [Fact]
    public async Task Created_sort_defaults_to_newest_first()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        await AssetsClient.CreateAssetAsync(tech, "AAA-0001", typeId, Token);
        await AssetsClient.CreateAssetAsync(tech, "BBB-0002", typeId, Token);
        await AssetsClient.CreateAssetAsync(tech, "CCC-0003", typeId, Token);

        (await AssetsClient.TagsAsync(tech, "sort=CreatedAt", Token))
            .ShouldBe(["CCC-0003", "BBB-0002", "AAA-0001"]);

        (await AssetsClient.TagsAsync(tech, "sort=CreatedAt&direction=Ascending", Token))
            .ShouldBe(["AAA-0001", "BBB-0002", "CCC-0003"]);
    }

    /// <summary>
    /// An asset that moves is an asset whose <c>updated_at</c> moves, which is what makes
    /// this sort different from creation order.
    /// </summary>
    [Fact]
    public async Task Updated_sort_puts_the_asset_that_last_moved_first()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var alice = await UserIdAsync("user");

        var first = await AssetsClient.CreateAssetAsync(tech, "AAA-0010", typeId, Token);
        await AssetsClient.CreateAssetAsync(tech, "BBB-0011", typeId, Token);
        (await AssetsClient.AssignAsync(tech, first.Id, alice, Token)).EnsureSuccessStatusCode();

        (await AssetsClient.TagsAsync(tech, "sort=UpdatedAt", Token)).ShouldBe(["AAA-0010", "BBB-0011"]);
    }

    /// <summary>
    /// A closed set rather than a free-text column name, so an unrecognised value is a 400
    /// from model binding and never a silent fallback to some other ordering.
    /// </summary>
    [Fact]
    public async Task An_unrecognised_sort_is_refused()
    {
        using var tech = await SignedInAsync("tech");

        var response = await tech.GetAsync(
            new Uri($"{AssetsClient.Assets}?sort=DropTable", UriKind.Relative),
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Status.ShouldBe(400);
    }

    [Fact]
    public async Task An_unrecognised_direction_is_refused()
    {
        using var tech = await SignedInAsync("tech");

        var response = await tech.GetAsync(
            new Uri($"{AssetsClient.Assets}?direction=Sideways", UriKind.Relative),
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Paging ----

    /// <summary>
    /// The envelope ARCHITECTURE.md §6 fixes, and the total that describes the whole query
    /// rather than the page.
    /// </summary>
    [Fact]
    public async Task The_page_envelope_describes_the_whole_query()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        for (var index = 1; index <= 7; index++)
        {
            await AssetsClient.CreateAssetAsync(tech, $"PAGE-{index:D2}", typeId, Token);
        }

        var page = await AssetsClient.ListAssetsAsync(tech, "page=2&pageSize=3", Token);

        page.Items.Select(asset => asset.AssetTag).ShouldBe(["PAGE-04", "PAGE-05", "PAGE-06"]);
        page.Total.ShouldBe(7);
        page.Page.ShouldBe(2);
        page.PageSize.ShouldBe(3);
    }

    /// <summary>
    /// Every ordering ends at the id. None of the sort columns but the tag is unique, and a
    /// paged list whose order changes between two reads of the same data silently drops and
    /// duplicates rows across page boundaries — WP-1.4 learned that from a test rather than
    /// from reasoning.
    /// </summary>
    [Fact]
    public async Task Paging_a_column_full_of_ties_loses_and_duplicates_nothing()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        // Twelve assets, all in stock, none with a warranty date: every sort column but the
        // tag is a tie across the whole set, so only the id tiebreaker makes paging stable.
        for (var index = 1; index <= 12; index++)
        {
            await AssetsClient.CreateAssetAsync(tech, $"TIE-{index:D2}", typeId, Token);
        }

        var seen = new List<string>();

        for (var page = 1; page <= 3; page++)
        {
            seen.AddRange(await AssetsClient.TagsAsync(tech, $"sort=Status&page={page}&pageSize=4", Token));
        }

        seen.Count.ShouldBe(12);
        seen.Distinct(StringComparer.Ordinal).Count().ShouldBe(12);
    }

    /// <summary>Out-of-range paging is clamped rather than rejected, per <c>PageRequest</c>.</summary>
    [Fact]
    public async Task An_out_of_range_page_size_is_clamped()
    {
        using var tech = await SignedInAsync("tech");

        var page = await AssetsClient.ListAssetsAsync(tech, "page=0&pageSize=100000", Token);

        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(200);
    }

    /// <summary>A page past the end is empty, and still reports the real total.</summary>
    [Fact]
    public async Task A_page_past_the_end_is_empty_but_still_counts()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        await AssetsClient.CreateAssetAsync(tech, "ONLY-0001", typeId, Token);

        var page = await AssetsClient.ListAssetsAsync(tech, "page=9&pageSize=25", Token);

        page.Items.ShouldBeEmpty();
        page.Total.ShouldBe(1);
    }

    // ---- The row shape ----

    /// <summary>
    /// Every field a list screen draws is on the row, so the list is one round trip and
    /// never a row followed by a lookup per row.
    /// </summary>
    [Fact]
    public async Task A_row_carries_everything_a_screen_draws()
    {
        using var tech = await SignedInAsync("tech");
        using var admin = await SignedInAsync("admin");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var alice = await UserIdAsync("user");

        var finance = await DirectoryClient.CreateDepartmentAsync(admin, "Payroll", "PAY", Token);
        var org = await DirectoryClient.CreateLocationAsync(admin, "Northgate Group", "Organization", null, Token);
        var site = await DirectoryClient.CreateLocationAsync(admin, "Northgate", "Site", org.Id, Token);

        var created = await AssetsClient.CreateDetailedAsync(
            tech,
            new
            {
                assetTag = "FULL-0001",
                assetTypeId = typeId,
                name = "Alice's laptop",
                serialNumber = "SN-FULL-1",
                manufacturer = "Lenovo",
                model = "T14",
                departmentId = finance.Id,
                locationId = site.Id,
                warrantyExpiresAt = "2029-01-31",
                cost = 1234.56m,
            },
            Token);

        (await AssetsClient.AssignAsync(tech, created.Id, alice, Token)).EnsureSuccessStatusCode();

        var row = (await AssetsClient.ListAssetsAsync(tech, "search=FULL-0001", Token)).Items.Single();

        row.Id.ShouldBe(created.Id);
        row.AssetTag.ShouldBe("FULL-0001");
        row.Name.ShouldBe("Alice's laptop");
        row.SerialNumber.ShouldBe("SN-FULL-1");
        row.Manufacturer.ShouldBe("Lenovo");
        row.Model.ShouldBe("T14");
        row.AssetTypeName.ShouldNotBeNullOrWhiteSpace();

        // Issued out of stock, so the row reads Deployed — the status the assignment moved it to.
        row.AssetStatusCode.ShouldBe(AssetStatusCode.Deployed);
        row.AssetStatusName.ShouldNotBeNullOrWhiteSpace();

        row.AssignedToUserId.ShouldBe(alice);
        row.AssignedToUserName.ShouldNotBeNullOrWhiteSpace();
        row.DepartmentId.ShouldBe(finance.Id);
        row.DepartmentName.ShouldBe("Payroll");
        row.LocationId.ShouldBe(site.Id);
        row.LocationPath.ShouldNotBeNullOrWhiteSpace();
        row.WarrantyExpiresAt.ShouldBe(new DateOnly(2029, 1, 31));
        row.CreatedAt.ShouldBe(created.Id == row.Id ? row.CreatedAt : default);
        row.UpdatedAt.ShouldBeGreaterThanOrEqualTo(row.CreatedAt);
    }

    /// <summary>
    /// A rename of the type or the status reaches every row, because those names are joined
    /// in at read time rather than cached on the asset — the opposite of the department and
    /// location strings, which §3 rule 6 forbids a foreign key for.
    /// </summary>
    [Fact]
    public async Task Renaming_a_type_reaches_every_row()
    {
        using var tech = await SignedInAsync("tech");
        using var admin = await SignedInAsync("admin");

        var type = await AssetsClient.CreateTypeAsync(admin, "Handheld", 910, Token);
        await AssetsClient.CreateAssetAsync(tech, "HAND-0001", type.Id, Token);

        var rename = await ApiClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{AssetsClient.Types}/{type.Id}",
            new { name = "Rugged Handheld", description = (string?)null, sortOrder = 910 },
            Token);
        rename.EnsureSuccessStatusCode();

        var row = (await AssetsClient.ListAssetsAsync(tech, "search=HAND-0001", Token)).Items.Single();

        row.AssetTypeName.ShouldBe("Rugged Handheld");
    }

    /// <summary>
    /// Filters compose. Two narrowings are an intersection, not a union — the warranty pair
    /// is the deliberate exception and is asserted above.
    /// </summary>
    [Fact]
    public async Task Filters_narrow_each_other()
    {
        using var tech = await SignedInAsync("tech");
        var world = await EstateAsync(tech);

        var tags = await AssetsClient.TagsAsync(
            tech,
            $"statusCode={AssetStatusCode.InStock}&search=STOCK-1&assetTypeId={world}",
            Token);

        tags.ShouldBe(["STOCK-1"]);
    }

    /// <summary>
    /// A small estate covering four of the six lifecycle codes, so the status filters and
    /// the status sort have something to be wrong about.
    /// </summary>
    /// <param name="tech">A signed-in technician.</param>
    /// <returns>The asset type every asset in it carries.</returns>
    private async Task<Guid> EstateAsync(HttpClient tech)
    {
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var alice = await UserIdAsync("user");

        await AssetsClient.CreateAssetAsync(tech, "STOCK-1", typeId, Token);
        await AssetsClient.CreateAssetAsync(tech, "STOCK-2", typeId, Token);

        var deployed = await AssetsClient.CreateAssetAsync(tech, "DESK-1", typeId, Token);
        (await AssetsClient.AssignAsync(tech, deployed.Id, alice, Token)).EnsureSuccessStatusCode();

        var repair = await AssetsClient.CreateAssetAsync(tech, "WORKSHOP-1", typeId, Token);
        (await AssetsClient.SendForRepairAsync(tech, repair.Id, Token)).EnsureSuccessStatusCode();

        var retired = await AssetsClient.CreateAssetAsync(tech, "GONE-1", typeId, Token);
        (await AssetsClient.RetireAsync(tech, retired.Id, Token)).EnsureSuccessStatusCode();

        return typeId;
    }

    /// <summary>
    /// Six assets straddling every boundary the warranty filters have: before the window, on
    /// each edge of it, past it, and with no date at all.
    /// </summary>
    /// <remarks>
    /// The dates are relative to the machine's own UTC date rather than fixed, because the
    /// handler reads <c>IClock</c> and the boundary being asserted is "today" — a fixture
    /// with hard-coded dates would assert something different every day it ran.
    /// </remarks>
    /// <param name="tech">A signed-in technician.</param>
    /// <param name="today">The date the assets are placed around.</param>
    private static async Task WarrantyEstateAsync(HttpClient tech, DateOnly today)
    {
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        (string Tag, DateOnly? Expiry)[] assets =
        [
            ("LAPSED", today.AddDays(-1)),
            ("TODAY", today),
            ("DAY-7", today.AddDays(7)),
            ("DAY-30", today.AddDays(30)),
            ("DAY-31", today.AddDays(31)),
            ("NONE", null),
        ];

        foreach (var (tag, expiry) in assets)
        {
            await AssetsClient.CreateDetailedAsync(
                tech,
                new
                {
                    assetTag = tag,
                    assetTypeId = typeId,
                    warrantyExpiresAt = expiry?.ToString("yyyy-MM-dd", null),
                },
                Token);
        }
    }

    private async Task<Guid> UserIdAsync(string userName)
    {
        using var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        return (await AuthClient.ReadUserAsync(response, Token)).Id;
    }

    private Task<HttpClient> SignedInAsync(string userName) =>
        AuthClient.SignedInAsync(fixture, userName, Token);
}
