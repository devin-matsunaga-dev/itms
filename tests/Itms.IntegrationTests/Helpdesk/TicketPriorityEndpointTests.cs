using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// The ticket-priority endpoints over the wire: the two identifiers, the immutable code,
/// the SLA-target bounds, retirement instead of deletion, and the role boundary.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketPriorityEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_admin_creates_a_priority_and_can_read_it_back()
    {
        using var admin = await SignedInAsync("admin");

        var created = await HelpdeskClient.CreatePriorityAsync(admin, "urgent", "Urgent", 5, Token);

        created.Code.ShouldBe("urgent");
        created.Rank.ShouldBe(5);
        created.ResponseTargetMinutes.ShouldBe(60);
        created.ResolutionTargetMinutes.ShouldBe(480);
        created.IsActive.ShouldBeTrue();

        var fetched = await admin.GetAsync(new Uri($"{HelpdeskClient.Priorities}/{created.Id}", UriKind.Relative), Token);
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadAsync<TicketPriorityDto>(fetched, Token)).Code.ShouldBe("urgent");
    }

    [Fact]
    public async Task A_code_is_lower_cased_on_the_way_in()
    {
        using var admin = await SignedInAsync("admin");

        var created = await HelpdeskClient.CreatePriorityAsync(admin, "URGENT", "Urgent", 5, Token);

        created.Code.ShouldBe("urgent");
    }

    [Fact]
    public async Task A_duplicate_code_is_refused()
    {
        using var admin = await SignedInAsync("admin");

        var response = await Post(admin, code: "critical", name: "Sev 1", rank: 5, response: 15, resolution: 240);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.duplicate_priority_code");
    }

    [Fact]
    public async Task A_duplicate_name_is_refused_regardless_of_case()
    {
        using var admin = await SignedInAsync("admin");

        var response = await Post(admin, code: "sev1", name: "critical", rank: 5, response: 15, resolution: 240);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.duplicate_priority_name");
    }

    [Theory]
    [InlineData("1urgent")]
    [InlineData("very urgent")]
    [InlineData("very_urgent")]
    [InlineData("")]
    public async Task A_malformed_code_is_a_validation_failure(string code)
    {
        using var admin = await SignedInAsync("admin");

        var response = await Post(admin, code, "Urgent", rank: 5, response: 60, resolution: 480);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Errors!.ShouldContainKey("code");
    }

    /// <summary>
    /// The invariant WP-1.8 depends on: a resolution due before the response is a breach
    /// no amount of work could avoid.
    /// </summary>
    [Fact]
    public async Task A_resolution_target_sooner_than_the_response_target_is_a_validation_failure()
    {
        using var admin = await SignedInAsync("admin");

        var response = await Post(admin, "urgent", "Urgent", rank: 5, response: 480, resolution: 60);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Errors!.ShouldContainKey("resolutionTargetMinutes");
    }

    [Theory]
    [InlineData(0, 480)]
    [InlineData(60, 0)]
    public async Task A_target_of_zero_is_a_validation_failure(int responseMinutes, int resolutionMinutes)
    {
        using var admin = await SignedInAsync("admin");

        var response = await Post(admin, "urgent", "Urgent", rank: 5, responseMinutes, resolutionMinutes);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The code is the key colours, integrations, and later rules resolve against, so the
    /// edit shape does not carry it: a request that names one has that field ignored, and
    /// the stored code is unchanged.
    /// </summary>
    [Fact]
    public async Task An_update_renames_the_priority_and_cannot_move_its_code()
    {
        using var admin = await SignedInAsync("admin");
        var critical = await Priority(admin, "critical");

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{HelpdeskClient.Priorities}/{critical.Id}",
            new
            {
                code = "something-else",
                name = "Sev 1",
                description = "Service is down.",
                rank = 1,
                responseTargetMinutes = 10,
                resolutionTargetMinutes = 120,
            },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ApiClient.ReadAsync<TicketPriorityDto>(response, Token);

        updated.Id.ShouldBe(critical.Id);
        updated.Name.ShouldBe("Sev 1");
        updated.ResponseTargetMinutes.ShouldBe(10);
        updated.ResolutionTargetMinutes.ShouldBe(120);
        updated.Code.ShouldBe("critical");

        (await Priority(admin, "critical")).Name.ShouldBe("Sev 1");
    }

    [Fact]
    public async Task An_update_with_an_inverted_pair_of_targets_is_refused()
    {
        using var admin = await SignedInAsync("admin");
        var critical = await Priority(admin, "critical");

        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{HelpdeskClient.Priorities}/{critical.Id}",
            new
            {
                name = "Critical",
                description = (string?)null,
                rank = 1,
                responseTargetMinutes = 480,
                resolutionTargetMinutes = 60,
            },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Refused means unchanged, not partially applied.
        (await Priority(admin, "critical")).ResponseTargetMinutes.ShouldBe(15);
    }

    [Fact]
    public async Task There_is_no_delete_route_for_a_priority()
    {
        using var admin = await SignedInAsync("admin");
        var low = await Priority(admin, "low");

        var response = await ApiClient.SendAsync(
            admin, HttpMethod.Delete, $"{HelpdeskClient.Priorities}/{low.Id}", null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task A_retired_priority_leaves_the_default_list_but_still_exists()
    {
        using var admin = await SignedInAsync("admin");
        var low = await Priority(admin, "low");

        var retire = await ApiClient.SendAsync(
            admin, HttpMethod.Post, $"{HelpdeskClient.Priorities}/{low.Id}/deactivate", null, Token);
        retire.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await ApiClient.ListAsync<TicketPriorityDto>(admin, HelpdeskClient.Priorities, Token)).Total.ShouldBe(3);
        (await ApiClient.ListAsync<TicketPriorityDto>(
            admin, $"{HelpdeskClient.Priorities}?includeInactive=true", Token)).Total.ShouldBe(4);

        var reinstate = await ApiClient.SendAsync(
            admin, HttpMethod.Post, $"{HelpdeskClient.Priorities}/{low.Id}/reactivate", null, Token);
        reinstate.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ApiClient.ListAsync<TicketPriorityDto>(admin, HelpdeskClient.Priorities, Token)).Total.ShouldBe(4);
    }

    /// <summary>
    /// Rank is not unique, so two priorities may share one. The list must still come back
    /// in the same order every time, which is what the name tie-break is for.
    /// </summary>
    [Fact]
    public async Task Priorities_sharing_a_rank_are_still_ordered_deterministically()
    {
        using var admin = await SignedInAsync("admin");

        await HelpdeskClient.CreatePriorityAsync(admin, "aardvark", "Aardvark", 1, Token);

        var first = await ApiClient.ListAsync<TicketPriorityDto>(admin, HelpdeskClient.Priorities, Token);
        var second = await ApiClient.ListAsync<TicketPriorityDto>(admin, HelpdeskClient.Priorities, Token);

        first.Items.Select(item => item.Code).ShouldBe(second.Items.Select(item => item.Code));
        first.Items[0].Code.ShouldBe("aardvark");
        first.Items[1].Code.ShouldBe("critical");
    }

    [Fact]
    public async Task An_unknown_priority_is_a_404()
    {
        using var admin = await SignedInAsync("admin");

        var response = await admin.GetAsync(
            new Uri($"{HelpdeskClient.Priorities}/{Guid.CreateVersion7()}", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.priority_not_found");
    }

    [Theory]
    [InlineData("tech")]
    [InlineData("user")]
    public async Task A_non_admin_may_read_priorities_but_not_create_one(string userName)
    {
        using var caller = await SignedInAsync(userName);

        var read = await caller.GetAsync(new Uri(HelpdeskClient.Priorities, UriKind.Relative), Token);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);

        var write = await Post(caller, "urgent", "Urgent", rank: 5, response: 60, resolution: 480);

        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        write.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task An_anonymous_caller_is_challenged()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync(new Uri(HelpdeskClient.Priorities, UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static Task<HttpResponseMessage> Post(
        HttpClient client,
        string code,
        string name,
        int rank,
        int response,
        int resolution) =>
        ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            HelpdeskClient.Priorities,
            new
            {
                code,
                name,
                description = (string?)null,
                rank,
                responseTargetMinutes = response,
                resolutionTargetMinutes = resolution,
            },
            Token);

    private async Task<HttpClient> SignedInAsync(string userName)
    {
        var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>Finds a seeded priority by code, so a test can act on one without creating it.</summary>
    private static async Task<TicketPriorityDto> Priority(HttpClient client, string code)
    {
        var page = await ApiClient.ListAsync<TicketPriorityDto>(
            client, $"{HelpdeskClient.Priorities}?includeInactive=true&pageSize=200", Token);

        return page.Items.SingleOrDefault(item => item.Code == code)
            ?? throw new InvalidOperationException($"The seed does not contain a priority with the code '{code}'.");
    }
}
