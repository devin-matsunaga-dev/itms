using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// The reference-data seed, asserted against a booted host.
/// </summary>
/// <remarks>
/// <para>
/// These rows are seeded in every environment, not only Development, because a deployment
/// with no priorities could not accept a ticket. <c>ResetAsync</c> truncates and then
/// re-runs the seeder, exactly as it does for the identity accounts — which is what makes
/// the seed something the suite can assert on without arranging anything.
/// </para>
/// <para>
/// The SLA targets are asserted because they are the numbers WP-1.8 will compute against,
/// and a silent edit to the seed would change behaviour nobody had asked to change. They
/// are still configuration: an administrator may edit any of them.
/// </para>
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class ReferenceDataSeedTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    /// <summary>The categories SPEC.md §2 names, in the order it names them.</summary>
    /// <remarks>Spelled out here rather than read from the seeder, so a silent edit to the seed fails this.</remarks>
    private static readonly string[] SeededCategories =
    [
        "Hardware", "Software", "Network", "Account/Access",
        "Microsoft 365", "Printer", "Security", "Other",
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task The_eight_categories_from_the_spec_are_seeded_in_order()
    {
        using var client = await SignedInAsync("user");

        var page = await ApiClient.ListAsync<TicketCategoryDto>(
            client, $"{HelpdeskClient.Categories}?pageSize=200", Token);

        page.Total.ShouldBe(SeededCategories.Length);
        page.Items.Select(item => item.Name).ShouldBe(SeededCategories);
        page.Items.ShouldAllBe(item => item.IsActive);
    }

    [Fact]
    public async Task The_four_priorities_from_the_spec_are_seeded_most_urgent_first()
    {
        using var client = await SignedInAsync("user");

        var page = await ApiClient.ListAsync<TicketPriorityDto>(
            client, $"{HelpdeskClient.Priorities}?pageSize=200", Token);

        page.Total.ShouldBe(4);
        page.Items.Select(item => item.Code).ShouldBe(["critical", "high", "medium", "low"]);
        page.Items.Select(item => item.Name).ShouldBe(["Critical", "High", "Medium", "Low"]);
        page.Items.Select(item => item.Rank).ShouldBe([1, 2, 3, 4]);
    }

    [Theory]
    [InlineData("critical", 15, 240)]
    [InlineData("high", 60, 480)]
    [InlineData("medium", 240, 1440)]
    [InlineData("low", 480, 4320)]
    public async Task Each_seeded_priority_carries_its_response_and_resolution_target(
        string code,
        int responseMinutes,
        int resolutionMinutes)
    {
        using var client = await SignedInAsync("user");

        var page = await ApiClient.ListAsync<TicketPriorityDto>(
            client, $"{HelpdeskClient.Priorities}?pageSize=200", Token);
        var priority = page.Items.Single(item => item.Code == code);

        priority.ResponseTargetMinutes.ShouldBe(responseMinutes);
        priority.ResolutionTargetMinutes.ShouldBe(resolutionMinutes);
    }

    /// <summary>
    /// Every seeded priority has to satisfy the invariant the entity enforces, or the
    /// seed itself would be data no edit form could have produced.
    /// </summary>
    [Fact]
    public async Task No_seeded_priority_promises_resolution_before_response()
    {
        using var client = await SignedInAsync("user");

        var page = await ApiClient.ListAsync<TicketPriorityDto>(
            client, $"{HelpdeskClient.Priorities}?pageSize=200", Token);

        page.Items.ShouldAllBe(item => item.ResolutionTargetMinutes >= item.ResponseTargetMinutes);
    }

    /// <summary>
    /// The seed's ids are literals, so they are the same in every database, and re-running
    /// the seeder recognises a row it already created even after an administrator has
    /// renamed it.
    /// </summary>
    [Fact]
    public async Task The_seeded_ids_are_the_literals_the_migration_declares()
    {
        using var client = await SignedInAsync("user");

        var categories = await ApiClient.ListAsync<TicketCategoryDto>(
            client, $"{HelpdeskClient.Categories}?pageSize=200", Token);
        var priorities = await ApiClient.ListAsync<TicketPriorityDto>(
            client, $"{HelpdeskClient.Priorities}?pageSize=200", Token);

        categories.Items.Single(item => item.Name == "Hardware").Id
            .ShouldBe(new Guid("01a052f8-4f00-785d-b254-c82d3c95840f"));
        priorities.Items.Single(item => item.Code == "critical").Id
            .ShouldBe(new Guid("01a052f8-4f08-7f95-90d3-b78147148662"));
    }

    private async Task<HttpClient> SignedInAsync(string userName)
    {
        var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        return client;
    }
}
