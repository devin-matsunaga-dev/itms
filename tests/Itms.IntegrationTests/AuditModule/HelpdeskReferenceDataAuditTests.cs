using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Helpdesk;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.AuditModule;

/// <summary>
/// SPEC.md §15 counts administrative configuration changes as mandatory audit coverage,
/// and the ticket categories and priorities are configuration. Neither raises a domain
/// event, so both go through <c>IAuditWriter</c> — the escape hatch ARCHITECTURE.md §8
/// keeps for exactly this.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class HelpdeskReferenceDataAuditTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Creating_a_category_records_the_actor_the_address_and_the_fields()
    {
        var (admin, adminId) = await SignedInAsync("admin");
        using var client = admin;

        var category = await HelpdeskClient.CreateCategoryAsync(client, "Facilities", 90, Token);

        var row = (await Entries("TicketCategory", category.Id.ToString())).ShouldHaveSingleItem();

        row.Action.ShouldBe("helpdesk.category_created");
        row.ActorId.ShouldBe(adminId);
        row.ActorName.ShouldNotBeNullOrWhiteSpace();
        row.SourceIp.ShouldBe(IdentityWebFixture.RemoteIpAddress);
        row.Changes["name"].ShouldBe(new(null, "Facilities"));
        row.Changes["sortOrder"].ShouldBe(new(null, "90"));
    }

    [Fact]
    public async Task Renaming_a_category_records_only_the_fields_that_moved()
    {
        var (admin, _) = await SignedInAsync("admin");
        using var client = admin;

        var category = await HelpdeskClient.CreateCategoryAsync(client, "Facilities", 90, Token);

        var response = await ApiClient.SendAsync(
            client,
            HttpMethod.Put,
            $"{HelpdeskClient.Categories}/{category.Id}",
            new { name = "Facilities & Estates", description = (string?)null, sortOrder = 90 },
            Token);
        response.EnsureSuccessStatusCode();

        var rows = await Entries("TicketCategory", category.Id.ToString());
        var update = rows[^1];

        update.Action.ShouldBe("helpdesk.category_updated");
        update.Changes["name"].ShouldBe(new("Facilities", "Facilities & Estates"));
        update.Changes.ShouldNotContainKey("sortOrder");
        update.Changes.ShouldNotContainKey("description");
    }

    [Fact]
    public async Task Retiring_and_reinstating_a_category_are_separate_actions()
    {
        var (admin, _) = await SignedInAsync("admin");
        using var client = admin;

        var category = await HelpdeskClient.CreateCategoryAsync(client, "Facilities", 90, Token);

        await Status(client, $"{HelpdeskClient.Categories}/{category.Id}/deactivate");
        await Status(client, $"{HelpdeskClient.Categories}/{category.Id}/reactivate");

        var rows = await Entries("TicketCategory", category.Id.ToString());

        rows.Select(row => row.Action).ShouldBe([
            "helpdesk.category_created",
            "helpdesk.category_retired",
            "helpdesk.category_reinstated",
        ]);
        rows[1].Changes["isActive"].ShouldBe(new("true", "false"));
        rows[2].Changes["isActive"].ShouldBe(new("false", "true"));
    }

    /// <summary>
    /// Setting the state a row already has is a success, not a change — an entry saying
    /// so would be noise in the one table that has to stay readable.
    /// </summary>
    [Fact]
    public async Task Retiring_an_already_retired_category_writes_no_second_entry()
    {
        var (admin, _) = await SignedInAsync("admin");
        using var client = admin;

        var category = await HelpdeskClient.CreateCategoryAsync(client, "Facilities", 90, Token);

        await Status(client, $"{HelpdeskClient.Categories}/{category.Id}/deactivate");
        await Status(client, $"{HelpdeskClient.Categories}/{category.Id}/deactivate");

        (await Entries("TicketCategory", category.Id.ToString())).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Creating_a_priority_records_its_code_rank_and_targets()
    {
        var (admin, _) = await SignedInAsync("admin");
        using var client = admin;

        var priority = await HelpdeskClient.CreatePriorityAsync(client, "urgent", "Urgent", 5, Token);

        var row = (await Entries("TicketPriority", priority.Id.ToString())).ShouldHaveSingleItem();

        row.Action.ShouldBe("helpdesk.priority_created");
        row.Changes["code"].ShouldBe(new(null, "urgent"));
        row.Changes["rank"].ShouldBe(new(null, "5"));
        row.Changes["responseTargetMinutes"].ShouldBe(new(null, "60"));
        row.Changes["resolutionTargetMinutes"].ShouldBe(new(null, "480"));
    }

    /// <summary>
    /// An SLA target is the kind of configuration change SPEC.md §15 exists for: it
    /// changes what the system promises, and the trail has to say who changed it and
    /// from what.
    /// </summary>
    [Fact]
    public async Task Changing_an_SLA_target_records_the_before_and_after()
    {
        var (admin, _) = await SignedInAsync("admin");
        using var client = admin;

        var priority = await HelpdeskClient.CreatePriorityAsync(client, "urgent", "Urgent", 5, Token);

        var response = await ApiClient.SendAsync(
            client,
            HttpMethod.Put,
            $"{HelpdeskClient.Priorities}/{priority.Id}",
            new
            {
                name = "Urgent",
                description = (string?)null,
                rank = 5,
                responseTargetMinutes = 30,
                resolutionTargetMinutes = 480,
            },
            Token);
        response.EnsureSuccessStatusCode();

        var rows = await Entries("TicketPriority", priority.Id.ToString());
        var update = rows[^1];

        update.Action.ShouldBe("helpdesk.priority_updated");
        update.Changes["responseTargetMinutes"].ShouldBe(new("60", "30"));
        update.Changes.ShouldNotContainKey("resolutionTargetMinutes");
        update.Changes.ShouldNotContainKey("code");
    }

    /// <summary>
    /// The writer joins the caller's transaction, so a refused write must leave no entry
    /// claiming it happened. A duplicate name is refused after the entity is built and
    /// inside the transaction, which is the case worth proving.
    /// </summary>
    [Fact]
    public async Task A_refused_create_writes_no_audit_entry()
    {
        var (admin, _) = await SignedInAsync("admin");
        using var client = admin;

        var before = await ByAction("helpdesk.category_created");

        var response = await ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            HelpdeskClient.Categories,
            new { name = "network", description = (string?)null, sortOrder = 90 },
            Token);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await ByAction("helpdesk.category_created")).Count.ShouldBe(before.Count);
    }

    private static async Task Status(HttpClient client, string path)
    {
        var response = await ApiClient.SendAsync(client, HttpMethod.Post, path, null, Token);
        response.EnsureSuccessStatusCode();
    }

    private Task<IReadOnlyList<AuditRow>> Entries(string entityType, string entityId) =>
        AuditQueries.ByEntityAsync(fixture.DataSource, entityType, entityId, Token);

    private Task<IReadOnlyList<AuditRow>> ByAction(string action) =>
        AuditQueries.ByActionAsync(fixture.DataSource, action, Token);

    private async Task<(HttpClient Client, Guid UserId)> SignedInAsync(string userName)
    {
        var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        var user = await AuthClient.ReadUserAsync(response, Token);
        return (client, user.Id);
    }
}
