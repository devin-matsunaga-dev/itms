using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// The queue over the wire: filters, sorting, and paging.
/// </summary>
/// <remarks>
/// Every filter WP-1.5 names has a test that proves it narrows <em>and</em> that it leaves
/// the non-matching rows out — a filter asserted only by counting what it returns passes
/// just as happily when it does nothing at all.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketListEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_empty_queue_is_an_empty_page_and_not_a_404()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var page = await TicketClient.ListAsync(admin, string.Empty, Token);

        page.Items.ShouldBeEmpty();
        page.Total.ShouldBe(0);
        page.Page.ShouldBe(1);
    }

    [Fact]
    public async Task The_queue_defaults_to_newest_first()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, string.Empty, Token);

        page.Total.ShouldBe(4);
        page.Items.Select(t => t.Subject).ShouldBe(["Fourth", "Third", "Second", "First"]);
    }

    [Fact]
    public async Task Ascending_creation_order_is_the_reverse()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, "sort=CreatedAt&direction=Ascending", Token);

        page.Items.Select(t => t.Subject).ShouldBe(["First", "Second", "Third", "Fourth"]);
    }

    /// <summary>
    /// The technician queue ordering WP-1.9 will ask for: most urgent first, oldest first
    /// within a rank.
    /// </summary>
    [Fact]
    public async Task Sorting_by_priority_leads_with_the_most_urgent_and_breaks_ties_by_age()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, "sort=Priority", Token);

        // "Second" is the only one at the urgent priority; the rest share the other rank
        // and therefore come back oldest first.
        page.Items[0].Subject.ShouldBe("Second");
        page.Items.Skip(1).Select(t => t.Subject).ShouldBe(["First", "Third", "Fourth"]);
    }

    [Fact]
    public async Task Sorting_by_number_reads_as_a_person_would_expect()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, "sort=Number&direction=Ascending", Token);

        page.Items.Select(t => t.Number).ShouldBe(["TKT-0001", "TKT-0002", "TKT-0003", "TKT-0004"]);
    }

    [Fact]
    public async Task Filtering_by_status_returns_only_that_status()
    {
        var world = await WorldAsync();

        await TicketWriter.ParkAsync(fixture.DataSource, world.First.Id, TicketStatus.Waiting, Token);

        var waiting = await TicketClient.ListAsync(world.Admin, "status=Waiting", Token);
        var newOnes = await TicketClient.ListAsync(world.Admin, "status=New", Token);

        waiting.Total.ShouldBe(1);
        waiting.Items.Single().Subject.ShouldBe("First");
        newOnes.Total.ShouldBe(3);
        newOnes.Items.ShouldNotContain(t => t.Subject == "First");
    }

    /// <summary>
    /// "Open" is four statuses, not one, which is why the parameter repeats.
    /// </summary>
    [Fact]
    public async Task Several_statuses_may_be_asked_for_at_once()
    {
        var world = await WorldAsync();

        await TicketWriter.ParkAsync(fixture.DataSource, world.First.Id, TicketStatus.Waiting, Token);
        await TicketWriter.ParkAsync(fixture.DataSource, world.Second.Id, TicketStatus.Closed, Token);

        var page = await TicketClient.ListAsync(world.Admin, "status=New&status=Waiting", Token);

        page.Total.ShouldBe(3);
        page.Items.ShouldNotContain(t => t.Subject == "Second");
    }

    [Fact]
    public async Task Filtering_by_priority_returns_only_that_priority()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, $"priorityId={world.UrgentPriorityId}", Token);

        page.Total.ShouldBe(1);
        page.Items.Single().Subject.ShouldBe("Second");
    }

    [Fact]
    public async Task Filtering_by_category_returns_only_that_category()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, $"categoryId={world.Reference.CategoryId}", Token);

        page.Total.ShouldBe(4);

        var other = await TicketClient.ListAsync(world.Admin, $"categoryId={Guid.CreateVersion7()}", Token);
        other.Total.ShouldBe(0);
    }

    [Fact]
    public async Task Filtering_by_department_returns_only_that_department()
    {
        var world = await WorldAsync();

        var water = await TicketClient.ListAsync(world.Admin, $"departmentId={world.WaterDepartmentId}", Token);
        var power = await TicketClient.ListAsync(world.Admin, $"departmentId={world.PowerDepartmentId}", Token);

        water.Total.ShouldBe(3);
        power.Total.ShouldBe(1);
        power.Items.Single().Subject.ShouldBe("Fourth");
    }

    [Fact]
    public async Task Filtering_by_requester_returns_only_their_tickets()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, $"requesterId={world.UserId}", Token);

        page.Total.ShouldBe(1);
        page.Items.Single().Subject.ShouldBe("Third");
    }

    /// <summary>
    /// Nothing is assigned before WP-1.6, so the unassigned view is the whole queue — which
    /// is exactly the state the filter has to survive.
    /// </summary>
    [Fact]
    public async Task The_unassigned_filter_returns_the_tickets_nobody_holds()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, "unassigned=true", Token);

        page.Total.ShouldBe(4);
        page.Items.ShouldAllBe(t => t.AssigneeId == null);
    }

    [Fact]
    public async Task Filtering_by_an_assignee_nobody_matches_returns_nothing()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, $"assigneeId={Guid.CreateVersion7()}", Token);

        page.Total.ShouldBe(0);
    }

    [Fact]
    public async Task A_created_date_range_is_inclusive_at_both_ends()
    {
        var world = await WorldAsync();

        var all = await TicketClient.ListAsync(world.Admin, string.Empty, Token);
        var oldest = all.Items[^1].CreatedAt;
        var newest = all.Items[0].CreatedAt;

        var whole = await TicketClient.ListAsync(
            world.Admin,
            $"createdFrom={Encode(oldest)}&createdTo={Encode(newest)}",
            Token);

        whole.Total.ShouldBe(4);

        var after = await TicketClient.ListAsync(
            world.Admin,
            $"createdFrom={Encode(newest.AddMilliseconds(1))}",
            Token);

        after.Total.ShouldBe(0);
    }

    [Fact]
    public async Task An_inverted_date_range_matches_nothing_rather_than_failing()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(
            world.Admin,
            $"createdFrom={Encode(DateTimeOffset.UtcNow.AddDays(1))}&createdTo={Encode(DateTimeOffset.UtcNow.AddDays(-1))}",
            Token);

        page.Total.ShouldBe(0);
    }

    [Fact]
    public async Task Filters_combine_rather_than_replacing_one_another()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(
            world.Admin,
            $"status=New&departmentId={world.WaterDepartmentId}&priorityId={world.UrgentPriorityId}",
            Token);

        page.Total.ShouldBe(1);
        page.Items.Single().Subject.ShouldBe("Second");
    }

    [Fact]
    public async Task Paging_reports_the_total_across_every_page_and_never_repeats_a_row()
    {
        var world = await WorldAsync();

        var first = await TicketClient.ListAsync(world.Admin, "pageSize=2&page=1", Token);
        var second = await TicketClient.ListAsync(world.Admin, "pageSize=2&page=2", Token);

        first.Total.ShouldBe(4);
        second.Total.ShouldBe(4);
        first.Items.Count.ShouldBe(2);
        second.Items.Count.ShouldBe(2);
        first.Items.Select(t => t.Id).Intersect(second.Items.Select(t => t.Id)).ShouldBeEmpty();
    }

    /// <summary>Out-of-range paging is clamped, per WP-0.3's decision, not rejected.</summary>
    [Fact]
    public async Task An_absurd_page_size_is_clamped_rather_than_refused()
    {
        var world = await WorldAsync();

        var page = await TicketClient.ListAsync(world.Admin, "pageSize=100000&page=0", Token);

        page.PageSize.ShouldBe(200);
        page.Page.ShouldBe(1);
    }

    [Fact]
    public async Task An_unrecognised_sort_is_a_400_rather_than_a_silent_default()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var response = await admin.GetAsync(
            new Uri($"{TicketClient.Tickets}?sort=WhateverIsHandy", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_read_the_queue()
    {
        using var anonymous = fixture.CreateClient();

        var response = await anonymous.GetAsync(new Uri(TicketClient.Tickets, UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string Encode(DateTimeOffset value) =>
        Uri.EscapeDataString(value.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// Four tickets across two departments, two requesters, and two priorities — enough for
    /// every filter to have something it must exclude as well as something it must return.
    /// </summary>
    private async Task<World> WorldAsync()
    {
        var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);

        var water = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var power = await TicketClient.DepartmentAsync(admin, "Power Generation", Token);

        var urgent = await HelpdeskClient.CreatePriorityAsync(admin, "urgent-now", "Urgent Now", rank: 0, Token);
        var userId = await TicketClient.UserIdAsync(fixture, "user", Token);

        var first = await TicketClient.CreateAsync(admin, reference, water, "First", Token);
        var second = await TicketClient.CreateAsync(
            admin, reference, water, "Second", Token, priorityId: urgent.Id);
        var third = await TicketClient.CreateAsync(
            admin, reference, water, "Third", Token, requesterId: userId);
        var fourth = await TicketClient.CreateAsync(admin, reference, power, "Fourth", Token);

        return new World(admin, reference, water, power, urgent.Id, userId, first, second, third, fourth);
    }

    private sealed record World(
        HttpClient Admin,
        TicketWriter.ReferenceData Reference,
        Guid WaterDepartmentId,
        Guid PowerDepartmentId,
        Guid UrgentPriorityId,
        Guid UserId,
        TicketDetailDto First,
        TicketDetailDto Second,
        TicketDetailDto Third,
        TicketDetailDto Fourth);
}
