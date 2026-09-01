using System.Net;
using Itms.Contracts.Lookups;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.AssetsModule;
using Itms.IntegrationTests.Helpdesk;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.IntegrationTests.Identity;

/// <summary>
/// The user 360 page's panels — <c>GET /api/v1/users/{id}/assets</c> and
/// <c>/tickets</c> — and the rule about who may read whose.
/// </summary>
/// <remarks>
/// <para>
/// SPEC.md §4's acceptance shape is that finding a person immediately shows their
/// equipment and their support history. These are the two reads that make it possible, and
/// WP-2.5's own criterion is that each is a single round trip.
/// </para>
/// <para>
/// Both are aggregated by Identity through <c>IAssetLookup</c> and <c>ITicketLookup</c>,
/// which is why the boundary tests still pass with three modules now reading across it.
/// </para>
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class UserPanelTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Guid? _departmentId;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();
        _departmentId = null;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Somebody_holding_nothing_has_an_empty_equipment_panel()
    {
        using var tech = await SignedInAsync("tech");
        var userId = await UserIdAsync("user");

        (await AssetsOfAsync(tech, userId)).ShouldBeEmpty();
    }

    /// <summary>The "what is this person holding" reading the asset endpoints deliberately do not offer.</summary>
    [Fact]
    public async Task The_equipment_panel_lists_what_is_issued_to_that_person()
    {
        using var tech = await SignedInAsync("tech");
        var userId = await UserIdAsync("user");

        var laptop = await AssetAsync(tech, "LAP-0700");
        var phone = await AssetAsync(tech, "PHN-0700");
        await AssetAsync(tech, "LAP-0701");

        (await AssetsClient.AssignAsync(tech, laptop.Id, userId, Token)).EnsureSuccessStatusCode();
        (await AssetsClient.AssignAsync(tech, phone.Id, userId, Token)).EnsureSuccessStatusCode();

        var held = await AssetsOfAsync(tech, userId);

        held.Select(asset => asset.AssetTag).ShouldBe(["LAP-0700", "PHN-0700"]);
        held[0].AssignedToUserId.ShouldBe(userId);
    }

    /// <summary>Taking equipment back takes it off the panel, which is the whole point of reading it live.</summary>
    [Fact]
    public async Task Returning_equipment_takes_it_off_the_panel()
    {
        using var tech = await SignedInAsync("tech");
        var userId = await UserIdAsync("user");
        var laptop = await AssetAsync(tech, "LAP-0702");

        (await AssetsClient.AssignAsync(tech, laptop.Id, userId, Token)).EnsureSuccessStatusCode();
        (await AssetsOfAsync(tech, userId)).Count.ShouldBe(1);

        (await AssetsClient.AssignAsync(tech, laptop.Id, null, Token)).EnsureSuccessStatusCode();

        (await AssetsOfAsync(tech, userId)).ShouldBeEmpty();
    }

    /// <summary>
    /// SPEC.md §4 asks for open tickets and previous tickets as two lists. They are
    /// complementary, so the pair is the whole history and nothing appears in both.
    /// </summary>
    [Fact]
    public async Task The_ticket_panel_splits_open_from_past_and_the_two_are_complementary()
    {
        using var tech = await SignedInAsync("tech");
        var userId = await UserIdAsync("user");

        var open = await TicketAsync(tech, "Still broken", userId);
        var finished = await TicketAsync(tech, "Fixed last week", userId);

        await WalkToClosedAsync(tech, finished.Id);

        var openPage = await TicketsOfAsync(tech, userId, "state=Open");
        var pastPage = await TicketsOfAsync(tech, userId, "state=Past");
        var allPage = await TicketsOfAsync(tech, userId, string.Empty);

        openPage.Items.ShouldHaveSingleItem().Number.ShouldBe(open.Number);
        openPage.Items[0].IsOpen.ShouldBeTrue();

        pastPage.Items.ShouldHaveSingleItem().Number.ShouldBe(finished.Number);
        pastPage.Items[0].IsOpen.ShouldBeFalse();

        allPage.Total.ShouldBe(2);
        (openPage.Total + pastPage.Total).ShouldBe(allPage.Total);
    }

    /// <summary>
    /// The panel is about who <em>raised</em> the ticket, not who is working it — a
    /// technician's own queue is a different question with a different endpoint.
    /// </summary>
    [Fact]
    public async Task The_ticket_panel_reads_the_requester_not_the_assignee()
    {
        using var tech = await SignedInAsync("tech");
        var userId = await UserIdAsync("user");
        var techId = await UserIdAsync("tech");

        var theirs = await TicketAsync(tech, "Raised by the user", userId);
        await TicketClient.AssignsAsync(tech, theirs.Id, techId, Token);

        (await TicketsOfAsync(tech, userId, string.Empty)).Total.ShouldBe(1);
        (await TicketsOfAsync(tech, techId, string.Empty)).Total.ShouldBe(0);
    }

    /// <summary>
    /// The self-service half of the rule: an end user reads their own panels, which is the
    /// "my kit" view the asset endpoints deliberately do not offer them.
    /// </summary>
    [Fact]
    public async Task An_end_user_may_read_their_own_panels()
    {
        using var tech = await SignedInAsync("tech");
        var userId = await UserIdAsync("user");
        var laptop = await AssetAsync(tech, "LAP-0703");

        (await AssetsClient.AssignAsync(tech, laptop.Id, userId, Token)).EnsureSuccessStatusCode();
        await TicketAsync(tech, "Mine", userId);

        using var user = await SignedInAsync("user");

        (await AssetsOfAsync(user, userId)).ShouldHaveSingleItem().AssetTag.ShouldBe("LAP-0703");
        (await TicketsOfAsync(user, userId, string.Empty)).Total.ShouldBe(1);
    }

    /// <summary>
    /// The other half, and the failure case: naming somebody else is a 403 rather than a
    /// silent substitution or an empty page. A user id is not a secret, so the honest
    /// refusal is the useful one.
    /// </summary>
    [Theory]
    [InlineData("assets")]
    [InlineData("tickets")]
    public async Task An_end_user_reading_somebody_elses_panel_is_refused(string panel)
    {
        var techId = await UserIdAsync("tech");

        using var user = await SignedInAsync("user");
        var response = await user.GetAsync(new Uri($"/api/v1/users/{techId}/{panel}", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("identity.user_not_self");
    }

    [Theory]
    [InlineData("assets")]
    [InlineData("tickets")]
    public async Task An_anonymous_caller_is_refused(string panel)
    {
        using var anonymous = fixture.CreateClient();
        var userId = await UserIdAsync("user");

        var response = await anonymous.GetAsync(new Uri($"/api/v1/users/{userId}/{panel}", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// An id nobody has answers an empty panel rather than a 404: the profile read beside
    /// it is the route that says whether the person exists, and two endpoints answering the
    /// same question differently is how a client ends up asking both.
    /// </summary>
    [Fact]
    public async Task A_user_who_does_not_exist_has_empty_panels()
    {
        using var tech = await SignedInAsync("tech");
        var nobody = Guid.CreateVersion7();

        (await AssetsOfAsync(tech, nobody)).ShouldBeEmpty();
        (await TicketsOfAsync(tech, nobody, string.Empty)).Total.ShouldBe(0);
    }

    private static Task<IReadOnlyList<AssetSummary>> AssetsOfAsync(HttpClient client, Guid userId) =>
        ReadListAsync(client, $"/api/v1/users/{userId}/assets");

    private static async Task<IReadOnlyList<AssetSummary>> ReadListAsync(HttpClient client, string path)
    {
        ArgumentNullException.ThrowIfNull(client);

        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Token);
        response.EnsureSuccessStatusCode();

        return await ApiClient.ReadAsync<IReadOnlyList<AssetSummary>>(response, Token);
    }

    private static Task<PageDto<TicketSummaryDto>> TicketsOfAsync(HttpClient client, Guid userId, string query) =>
        ApiClient.ListAsync<TicketSummaryDto>(
            client,
            query.Length == 0
                ? $"/api/v1/users/{userId}/tickets"
                : $"/api/v1/users/{userId}/tickets?{query}",
            Token);

    private static async Task<AssetDto> AssetAsync(HttpClient client, string tag)
    {
        var typeId = await AssetsClient.AnyTypeIdAsync(client, Token);
        return await AssetsClient.CreateAssetAsync(client, tag, typeId, Token);
    }

    private static async Task WalkToClosedAsync(HttpClient client, Guid ticketId)
    {
        var technicianId = (await AuthClient.ReadUserAsync(await AuthClient.MeAsync(client, Token), Token)).Id;

        await TicketClient.AssignsAsync(client, ticketId, technicianId, Token);
        (await TicketClient.ChangeStatusAsync(client, ticketId, TicketStatus.InProgress, Token))
            .EnsureSuccessStatusCode();
        (await TicketClient.ChangeStatusAsync(client, ticketId, TicketStatus.Resolved, Token, "Replaced the battery."))
            .EnsureSuccessStatusCode();
        (await TicketClient.ChangeStatusAsync(client, ticketId, TicketStatus.Closed, Token))
            .EnsureSuccessStatusCode();
    }

    private async Task<TicketDetailDto> TicketAsync(HttpClient client, string subject, Guid requesterId)
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await DepartmentAsync();

        return await TicketClient.CreateAsync(client, reference, departmentId, subject, Token, requesterId);
    }

    private async Task<Guid> DepartmentAsync()
    {
        if (_departmentId is { } existing)
        {
            return existing;
        }

        using var admin = await SignedInAsync("admin");
        var created = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        _departmentId = created;
        return created;
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
