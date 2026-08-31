using System.Net;
using System.Net.Http.Json;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// The ticket-category endpoints over the wire: uniqueness, the rename that existing
/// tickets follow, retirement instead of deletion, and the role boundary SPEC.md §13
/// puts around administration.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketCategoryEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_admin_creates_a_category_and_can_read_it_back()
    {
        using var admin = await SignedInAsync("admin");

        var created = await HelpdeskClient.CreateCategoryAsync(admin, "Facilities", 90, Token);

        created.Name.ShouldBe("Facilities");
        created.SortOrder.ShouldBe(90);
        created.IsActive.ShouldBeTrue();

        var fetched = await admin.GetAsync(new Uri($"{HelpdeskClient.Categories}/{created.Id}", UriKind.Relative), Token);
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadAsync<TicketCategoryDto>(fetched, Token)).Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task Creation_returns_a_location_header_pointing_at_the_new_category()
    {
        using var admin = await SignedInAsync("admin");

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Post,
            HelpdeskClient.Categories,
            new { name = "Facilities", description = (string?)null, sortOrder = 90 },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ApiClient.ReadAsync<TicketCategoryDto>(response, Token);
        response.Headers.Location!.ToString().ShouldEndWith($"{HelpdeskClient.Categories}/{created.Id}");
    }

    /// <summary>Case-insensitively: the unique index sits on the normalised column.</summary>
    [Fact]
    public async Task A_duplicate_category_name_is_refused_regardless_of_case()
    {
        using var admin = await SignedInAsync("admin");

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Post,
            HelpdeskClient.Categories,
            new { name = "network", description = (string?)null, sortOrder = 90 },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.duplicate_category_name");
        problem.Detail!.ShouldContain("Network");
    }

    /// <summary>
    /// WP-1.1's first criterion. A ticket stores the category's id and no copy of its
    /// name, so a rename is a one-row write and every reference follows it — which is
    /// what this asserts: the id is unchanged and the name read back through it is new.
    /// WP-1.2 is what puts an actual ticket on the other end of that id.
    /// </summary>
    [Fact]
    public async Task A_rename_keeps_the_id_so_every_reference_to_it_follows()
    {
        using var admin = await SignedInAsync("admin");
        var before = await Category(admin, "Network");

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{HelpdeskClient.Categories}/{before.Id}",
            new { name = "Networking", description = "Connectivity and VPN.", sortOrder = before.SortOrder },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ApiClient.ReadAsync<TicketCategoryDto>(response, Token);

        updated.Id.ShouldBe(before.Id);
        updated.Name.ShouldBe("Networking");

        // Read back through the id a ticket would hold.
        var fetched = await admin.GetAsync(new Uri($"{HelpdeskClient.Categories}/{before.Id}", UriKind.Relative), Token);
        (await ApiClient.ReadAsync<TicketCategoryDto>(fetched, Token)).Name.ShouldBe("Networking");
    }

    [Fact]
    public async Task An_update_may_keep_the_category_its_own_name()
    {
        using var admin = await SignedInAsync("admin");
        var category = await Category(admin, "Printer");

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{HelpdeskClient.Categories}/{category.Id}",
            new { name = "Printer", description = "Printing and scanning.", sortOrder = 60 },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadAsync<TicketCategoryDto>(response, Token)).Description.ShouldBe("Printing and scanning.");
    }

    [Fact]
    public async Task An_update_onto_another_categorys_name_is_refused()
    {
        using var admin = await SignedInAsync("admin");
        var printer = await Category(admin, "Printer");

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{HelpdeskClient.Categories}/{printer.Id}",
            new { name = "Network", description = (string?)null, sortOrder = 60 },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// WP-1.1's second criterion, in its strongest form: there is no removal path at all,
    /// so a category in use cannot be removed and neither can one that is not.
    /// </summary>
    [Fact]
    public async Task There_is_no_delete_route_for_a_category()
    {
        using var admin = await SignedInAsync("admin");
        var category = await Category(admin, "Other");

        var response = await ApiClient.SendAsync(
            admin, HttpMethod.Delete, $"{HelpdeskClient.Categories}/{category.Id}", null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task A_retired_category_leaves_the_default_list_but_still_exists()
    {
        using var admin = await SignedInAsync("admin");
        var category = await Category(admin, "Printer");
        var activeBefore = (await ApiClient.ListAsync<TicketCategoryDto>(admin, HelpdeskClient.Categories, Token)).Total;

        var retire = await ApiClient.SendAsync(
            admin, HttpMethod.Post, $"{HelpdeskClient.Categories}/{category.Id}/deactivate", null, Token);
        retire.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var active = await ApiClient.ListAsync<TicketCategoryDto>(admin, HelpdeskClient.Categories, Token);
        active.Total.ShouldBe(activeBefore - 1);
        active.Items.ShouldNotContain(item => item.Id == category.Id);

        var all = await ApiClient.ListAsync<TicketCategoryDto>(
            admin, $"{HelpdeskClient.Categories}?includeInactive=true", Token);
        all.Total.ShouldBe(activeBefore);

        // The row is still there, which is what keeps an existing ticket's category
        // readable.
        var fetched = await admin.GetAsync(new Uri($"{HelpdeskClient.Categories}/{category.Id}", UriKind.Relative), Token);
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadAsync<TicketCategoryDto>(fetched, Token)).IsActive.ShouldBeFalse();

        var reinstate = await ApiClient.SendAsync(
            admin, HttpMethod.Post, $"{HelpdeskClient.Categories}/{category.Id}/reactivate", null, Token);
        reinstate.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ApiClient.ListAsync<TicketCategoryDto>(admin, HelpdeskClient.Categories, Token)).Total.ShouldBe(activeBefore);
    }

    [Fact]
    public async Task A_name_that_is_only_whitespace_is_a_validation_failure_not_a_conflict()
    {
        using var admin = await SignedInAsync("admin");

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Post,
            HelpdeskClient.Categories,
            new { name = "   ", description = (string?)null, sortOrder = 0 },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Errors!.ShouldContainKey("name");
    }

    [Fact]
    public async Task A_negative_sort_order_is_a_validation_failure()
    {
        using var admin = await SignedInAsync("admin");

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Post,
            HelpdeskClient.Categories,
            new { name = "Facilities", description = (string?)null, sortOrder = -1 },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Errors!.ShouldContainKey("sortOrder");
    }

    [Fact]
    public async Task An_unknown_category_is_a_404()
    {
        using var admin = await SignedInAsync("admin");

        var response = await admin.GetAsync(
            new Uri($"{HelpdeskClient.Categories}/{Guid.CreateVersion7()}", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.category_not_found");
    }

    /// <summary>
    /// Any signed-in account may read: an end user filing their own ticket has to pick a
    /// category. Only an admin may write.
    /// </summary>
    [Theory]
    [InlineData("tech")]
    [InlineData("user")]
    public async Task A_non_admin_may_read_categories_but_not_create_one(string userName)
    {
        using var caller = await SignedInAsync(userName);

        var read = await caller.GetAsync(new Uri(HelpdeskClient.Categories, UriKind.Relative), Token);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);

        var write = await ApiClient.SendAsync(
            caller,
            HttpMethod.Post,
            HelpdeskClient.Categories,
            new { name = "Shadow IT", description = (string?)null, sortOrder = 99 },
            Token);

        // 403, not a 404 disguise and not a redirect (ARCHITECTURE.md §6).
        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        write.Headers.Location.ShouldBeNull();
    }

    [Theory]
    [InlineData("tech")]
    [InlineData("user")]
    public async Task A_non_admin_cannot_retire_a_category(string userName)
    {
        using var admin = await SignedInAsync("admin");
        var category = await Category(admin, "Other");

        using var caller = await SignedInAsync(userName);

        var response = await ApiClient.SendAsync(
            caller, HttpMethod.Post, $"{HelpdeskClient.Categories}/{category.Id}/deactivate", null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_anonymous_caller_is_challenged()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync(new Uri(HelpdeskClient.Categories, UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// CONVENTIONS.md's security floor: a cookie-authenticated write without an
    /// antiforgery token is exactly the shape a hostile page exploits.
    /// </summary>
    [Fact]
    public async Task A_write_without_an_antiforgery_token_is_rejected()
    {
        using var admin = await SignedInAsync("admin");

        var response = await admin.PostAsJsonAsync(
            new Uri(HelpdeskClient.Categories, UriKind.Relative),
            new { name = "Facilities", description = (string?)null, sortOrder = 90 },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("auth.antiforgery_failed");
    }

    private async Task<HttpClient> SignedInAsync(string userName)
    {
        var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>Finds a seeded category by name, so a test can act on one without creating it.</summary>
    private static async Task<TicketCategoryDto> Category(HttpClient client, string name)
    {
        var page = await ApiClient.ListAsync<TicketCategoryDto>(
            client, $"{HelpdeskClient.Categories}?pageSize=200", Token);

        return page.Items.SingleOrDefault(item => item.Name == name)
            ?? throw new InvalidOperationException($"The seed does not contain a category named '{name}'.");
    }
}
