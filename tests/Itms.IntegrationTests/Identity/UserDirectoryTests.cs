using Itms.IntegrationTests.Api;

namespace Itms.IntegrationTests.Identity;

/// <summary>
/// The user directory as a picker actually asks for it.
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
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class UserDirectoryTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The exact path `fetchAssignableUsers` requests. Do not "tidy" it.</summary>
    private const string PickerQuery = "/api/v1/users?limit=200";

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

        people.ShouldNotBeEmpty();
        people.Select(person => person.Email).ShouldContain("admin@itms.local");
    }

    [Fact]
    public async Task A_blank_term_lists_every_active_account_rather_than_none()
    {
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var people = await ReadAsync(client, "/api/v1/users?search=");

        // The three seeded development accounts.
        people.Select(person => person.Email).OrderBy(email => email, StringComparer.Ordinal)
            .ShouldBe(["admin@itms.local", "tech@itms.local", "user@itms.local"]);
    }

    [Fact]
    public async Task Omitting_the_term_entirely_is_the_same_as_a_blank_one()
    {
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var omitted = await ReadAsync(client, "/api/v1/users");
        var blank = await ReadAsync(client, "/api/v1/users?search=");

        omitted.Count.ShouldBe(blank.Count);
    }

    [Fact]
    public async Task A_term_still_narrows_the_list()
    {
        // The fix must not have turned the filter off.
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var people = await ReadAsync(client, "/api/v1/users?search=toni");

        people.Select(person => person.Email).ShouldBe(["tech@itms.local"]);
    }

    [Fact]
    public async Task A_term_matching_nobody_is_still_an_empty_list()
    {
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var people = await ReadAsync(client, "/api/v1/users?search=nobodyhasthisname");

        people.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_listing_is_ordered_by_display_name()
    {
        // A picker whose order changes between two reads is a picker people mis-click.
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var people = await ReadAsync(client, PickerQuery);

        // Avery Admin, Toni Technician, Uma User.
        people.Select(person => person.DisplayName)
            .ShouldBe(["Avery Admin", "Toni Technician", "Uma User"]);
    }

    [Fact]
    public async Task Every_person_carries_the_roles_a_picker_has_to_filter_on()
    {
        // The client filters the *assignee* picker to staff, and can only do that if the
        // roles travel. WP-1.6 widened UserSummary for exactly this.
        using var client = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var people = await ReadAsync(client, PickerQuery);

        people.ShouldAllBe(person => person.Roles.Count > 0);
        people.Single(person => person.Email == "user@itms.local").Roles.ShouldBe(["User"]);
        people.Single(person => person.Email == "tech@itms.local").Roles.ShouldBe(["Technician"]);
    }

    private static async Task<IReadOnlyList<DirectoryUserDto>> ReadAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Token);
        response.EnsureSuccessStatusCode();

        return await ApiClient.ReadAsync<List<DirectoryUserDto>>(response, Token);
    }

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
