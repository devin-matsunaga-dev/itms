using Itms.IntegrationTests.Api;
using Itms.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Itms.IntegrationTests.Identity;

/// <summary>
/// The user directory as a picker actually asks for it, and as WP-2.7's directory screen
/// asks for it.
/// </summary>
/// <remarks>
/// <para>
/// This class exists because of a defect that survived from WP-0.5 to WP-1.14: a blank
/// search term returned an empty list, so every picker in the product — the ticket
/// assignee, the queue's assignee filter, the create form's requester — was permanently
/// empty. Assignment was impossible through the interface, and therefore so was moving a
/// ticket out of <c>New</c>.
/// </para>
/// <para>
/// <b>Nothing caught it, and the reason is the lesson.</b> `RoleAuthorizationTests` had
/// covered this endpoint since WP-0.5, but every one of its calls passed
/// <c>?search=a</c> — a term. The client passes none. The tests asked a question the
/// client never asks, and the question the client does ask went unasked for four
/// packages. **Every assertion here uses the query string the client actually sends.**
/// </para>
/// <para>
/// <b>WP-2.7 widened the route to a page and the tests with it.</b> The response is now the
/// <c>{ items, total, page, pageSize }</c> envelope every other list answers with, and
/// <c>?limit=</c> is gone in favour of <c>?pageSize=</c>. The picker query below moved with
/// the client's, and it is still the literal string the client sends.
/// </para>
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class UserDirectoryTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The exact path `fetchAssignableUsers` requests. Do not "tidy" it.</summary>
    private const string PickerQuery = "/api/v1/users?pageSize=200";

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task The_query_a_picker_opens_with_returns_people()
    {
        // The regression test. This is the call the client makes, verbatim.
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var people = await ReadAsync(client, PickerQuery);

        people.Items.ShouldNotBeEmpty();
        people.Items.Select(person => person.Email).ShouldContain("admin@itms.local");
    }

    [Fact]
    public async Task A_blank_term_lists_every_active_account_rather_than_none()
    {
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var people = await ReadAsync(client, "/api/v1/users?search=");

        // The three seeded development accounts.
        people.Items.Select(person => person.Email).OrderBy(email => email, StringComparer.Ordinal)
            .ShouldBe(["admin@itms.local", "tech@itms.local", "user@itms.local"]);
    }

    [Fact]
    public async Task Omitting_the_term_entirely_is_the_same_as_a_blank_one()
    {
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var omitted = await ReadAsync(client, "/api/v1/users");
        var blank = await ReadAsync(client, "/api/v1/users?search=");

        omitted.Total.ShouldBe(blank.Total);
    }

    [Fact]
    public async Task A_term_still_narrows_the_list()
    {
        // The fix must not have turned the filter off.
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var people = await ReadAsync(client, "/api/v1/users?search=toni");

        people.Items.Select(person => person.Email).ShouldBe(["tech@itms.local"]);
    }

    [Fact]
    public async Task A_term_matching_nobody_is_still_an_empty_page()
    {
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var people = await ReadAsync(client, "/api/v1/users?search=nobodyhasthisname");

        people.Items.ShouldBeEmpty();
        people.Total.ShouldBe(0);
    }

    [Fact]
    public async Task The_listing_is_ordered_by_display_name()
    {
        // A picker whose order changes between two reads is a picker people mis-click.
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var people = await ReadAsync(client, PickerQuery);

        // Avery Admin, Toni Technician, Uma User.
        people.Items.Select(person => person.DisplayName)
            .ShouldBe(["Avery Admin", "Toni Technician", "Uma User"]);
    }

    [Fact]
    public async Task Every_person_carries_the_roles_a_picker_has_to_filter_on()
    {
        // The client filters the *assignee* picker to staff, and can only do that if the
        // roles travel. WP-1.6 widened UserSummary for exactly this.
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var people = await ReadAsync(client, PickerQuery);

        people.Items.ShouldAllBe(person => person.Roles.Count > 0);
        people.Items.Single(person => person.Email == "user@itms.local").Roles.ShouldBe(["User"]);
        people.Items.Single(person => person.Email == "tech@itms.local").Roles.ShouldBe(["Technician"]);
    }

    /// <summary>The envelope is what makes a paged screen possible at all.</summary>
    [Fact]
    public async Task The_page_reports_the_total_beyond_it()
    {
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var first = await ReadAsync(client, "/api/v1/users?pageSize=1");

        first.Items.Count.ShouldBe(1);
        first.Total.ShouldBe(3);
        first.Page.ShouldBe(1);
        first.PageSize.ShouldBe(1);
    }

    /// <summary>
    /// Paging has to partition the directory: every account appears on exactly one page.
    /// </summary>
    [Fact]
    public async Task Consecutive_pages_do_not_overlap_or_drop_anybody()
    {
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var first = await ReadAsync(client, "/api/v1/users?pageSize=2&page=1");
        var second = await ReadAsync(client, "/api/v1/users?pageSize=2&page=2");

        first.Items.Count.ShouldBe(2);
        second.Items.Count.ShouldBe(1);

        first.Items.Select(person => person.Id)
            .Concat(second.Items.Select(person => person.Id))
            .Distinct()
            .Count()
            .ShouldBe(3);
    }

    [Fact]
    public async Task The_address_is_an_ordering_of_its_own()
    {
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var people = await ReadAsync(client, "/api/v1/users?sort=Email&direction=Descending");

        people.Items.Select(person => person.Email)
            .ShouldBe(["user@itms.local", "tech@itms.local", "admin@itms.local"]);
    }

    /// <summary>
    /// A deactivated account stays out of every picker, because equipment and tickets are
    /// not handed to somebody who can no longer sign in.
    /// </summary>
    [Fact]
    public async Task A_deactivated_account_is_absent_unless_it_is_asked_for()
    {
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);
        await DeactivateAsync("user");

        var byDefault = await ReadAsync(client, PickerQuery);
        var included = await ReadAsync(client, "/api/v1/users?pageSize=200&includeInactive=true");

        byDefault.Items.Select(person => person.Email).ShouldNotContain("user@itms.local");
        included.Items.Select(person => person.Email).ShouldContain("user@itms.local");
        included.Items.Single(person => person.Email == "user@itms.local").IsActive.ShouldBeFalse();
    }

    /// <summary>
    /// The filter behind the directory's "who is in Finance" question. The department is a
    /// bare identifier with no foreign key (§3 rule 6), which is why a test can place
    /// somebody in one without Directory having a row for it.
    /// </summary>
    [Fact]
    public async Task The_directory_narrows_to_a_department_and_a_location()
    {
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var departmentId = Guid.CreateVersion7();
        var locationId = Guid.CreateVersion7();
        await PlaceAsync("tech", departmentId, locationId);

        var byDepartment = await ReadAsync(client, $"/api/v1/users?departmentId={departmentId}");
        var byLocation = await ReadAsync(client, $"/api/v1/users?locationId={locationId}");
        var byOtherDepartment = await ReadAsync(client, $"/api/v1/users?departmentId={Guid.CreateVersion7()}");

        byDepartment.Items.Select(person => person.Email).ShouldBe(["tech@itms.local"]);
        byLocation.Items.Select(person => person.Email).ShouldBe(["tech@itms.local"]);
        byOtherDepartment.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_directory_narrows_to_a_role()
    {
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var technicians = await ReadAsync(client, "/api/v1/users?role=Technician");

        technicians.Items.Select(person => person.Email).ShouldBe(["tech@itms.local"]);
    }

    /// <summary>
    /// The role is matched on the normalised name, so the casing in a hand-written URL
    /// does not decide whether anybody comes back.
    /// </summary>
    [Fact]
    public async Task The_role_filter_ignores_casing()
    {
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var people = await ReadAsync(client, "/api/v1/users?role=technician");

        people.Items.Select(person => person.Email).ShouldBe(["tech@itms.local"]);
    }

    /// <summary>
    /// An unrecognised role is a filter matching nothing rather than a 400 — the reading
    /// WP-2.3 settled for an unrecognised asset status code.
    /// </summary>
    [Fact]
    public async Task An_unrecognised_role_matches_nobody_rather_than_failing()
    {
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var people = await ReadAsync(client, "/api/v1/users?role=Superuser");

        people.Items.ShouldBeEmpty();
        people.Total.ShouldBe(0);
    }

    [Fact]
    public async Task Two_filters_narrow_rather_than_widen()
    {
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var departmentId = Guid.CreateVersion7();
        await PlaceAsync("tech", departmentId, locationId: null);

        var contradiction = await ReadAsync(client, $"/api/v1/users?departmentId={departmentId}&role=Admin");

        contradiction.Items.ShouldBeEmpty();
    }

    /// <summary>Places a seeded account in a department and a location.</summary>
    private async Task PlaceAsync(string userName, Guid? departmentId, Guid? locationId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ItmsUser>>();
        var account = await users.FindByNameAsync(userName);

        account!.PlaceIn(departmentId, locationId, DateTimeOffset.UtcNow, actor: null);
        (await users.UpdateAsync(account)).Succeeded.ShouldBeTrue();
    }

    /// <summary>Stops a seeded account signing in, without deleting anything (invariant 9).</summary>
    private async Task DeactivateAsync(string userName)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ItmsUser>>();
        var account = await users.FindByNameAsync(userName);

        account!.Deactivate(DateTimeOffset.UtcNow, actor: null);
        (await users.UpdateAsync(account)).Succeeded.ShouldBeTrue();
    }

    private static async Task<DirectoryPageDto> ReadAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Token);
        response.EnsureSuccessStatusCode();

        return await ApiClient.ReadAsync<DirectoryPageDto>(response, Token);
    }

    /// <summary>The page envelope as it arrives on the wire.</summary>
    private sealed record DirectoryPageDto(
        IReadOnlyList<DirectoryUserDto> Items,
        int Total,
        int Page,
        int PageSize);

    /// <summary>`UserSummary` as it arrives on the wire. It carries no user name.</summary>
    private sealed record DirectoryUserDto(
        Guid Id,
        string DisplayName,
        string Email,
        Guid? DepartmentId,
        Guid? LocationId,
        bool IsActive,
        IReadOnlyList<string> Roles);
}
