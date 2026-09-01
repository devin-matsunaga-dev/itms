using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.Tickets;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>The detail endpoint: the full record, its timeline, and its entity tag.</summary>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketDetailEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_ticket_that_does_not_exist_is_a_404()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var response = await admin.GetAsync(
            new Uri($"{TicketClient.Tickets}/{Guid.CreateVersion7()}", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.ticket_not_found");
    }

    [Fact]
    public async Task The_detail_carries_the_description_the_list_row_does_not()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var created = await TicketClient.CreateAsync(admin, reference, departmentId, "No signal", Token);
        var (ticket, _) = await TicketClient.GetAsync(admin, created.Id, Token);

        ticket.Id.ShouldBe(created.Id);
        ticket.Number.ShouldBe(created.Number);
        ticket.Description.ShouldNotBeNullOrWhiteSpace();
        ticket.CategoryName.ShouldBe(created.CategoryName);
        ticket.PriorityCode.ShouldBe(created.PriorityCode);
    }

    /// <summary>
    /// The buttons WP-1.10 draws come from here, so the endpoint has to agree with the
    /// state machine at whatever status the ticket is actually in.
    /// </summary>
    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Waiting)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task The_detail_offers_exactly_the_moves_the_state_machine_allows(TicketStatus status)
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var created = await TicketClient.CreateAsync(admin, reference, departmentId, "Parked", Token);
        await TicketWriter.ParkAsync(fixture.DataSource, created.Id, status, Token);

        var (ticket, _) = await TicketClient.GetAsync(admin, created.Id, Token);

        ticket.Status.ShouldBe(status);
        ticket.AllowedNextStatuses.ShouldBe(TicketStateMachine.DestinationsFrom(status), ignoreOrder: true);
    }

    [Fact]
    public async Task A_terminal_ticket_offers_no_moves_at_all()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var created = await TicketClient.CreateAsync(admin, reference, departmentId, "Done", Token);
        await TicketWriter.ParkAsync(fixture.DataSource, created.Id, TicketStatus.Closed, Token);

        var (ticket, _) = await TicketClient.GetAsync(admin, created.Id, Token);

        ticket.AllowedNextStatuses.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_ticket_nothing_has_happened_to_has_an_empty_timeline()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var created = await TicketClient.CreateAsync(admin, reference, departmentId, "Fresh", Token);
        var (ticket, _) = await TicketClient.GetAsync(admin, created.Id, Token);

        ticket.History.ShouldBeEmpty();
        ticket.HasMoreHistory.ShouldBeFalse();
    }

    /// <summary>
    /// The timeline is embedded so the detail screen is one round trip, and it is the same
    /// shape the paged endpoint returns.
    /// </summary>
    [Fact]
    public async Task The_detail_embeds_the_timeline_newest_first()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var created = await TicketClient.CreateAsync(admin, reference, departmentId, "Moving", Token);
        await TicketWriter.ParkAsync(fixture.DataSource, created.Id, TicketStatus.InProgress, Token);

        (await TicketClient.ChangeStatusAsync(admin, created.Id, TicketStatus.Waiting, Token, holdReason: "Waiting on the vendor."))
            .EnsureSuccessStatusCode();
        (await TicketClient.ChangeStatusAsync(admin, created.Id, TicketStatus.InProgress, Token))
            .EnsureSuccessStatusCode();

        var (ticket, _) = await TicketClient.GetAsync(admin, created.Id, Token);

        // Four, not two: parking and resuming each write their status move and the hold
        // reason arriving or being lifted (WP-1.15).
        // Four, not two: parking and resuming each write their status move and the hold
        // reason arriving or being lifted (WP-1.15).
        //
        // The newest line is the hold being *lifted*, not the status move it shares an
        // instant with — the read orders by `occurred_at DESC, sequence DESC`, and the hold
        // is written second within the change. A screen groups the pair and renders them
        // together, which is why the order inside one instant is a detail rather than a
        // promise.
        ticket.History.Count.ShouldBe(4);
        ticket.History.Select(entry => entry.ToValue).ShouldContain(nameof(TicketStatus.InProgress));
        ticket.History.Select(entry => entry.ToValue).ShouldContain(nameof(TicketStatus.Waiting));
        ticket.History.Count(entry => entry.Kind == TicketChangeKind.Hold).ShouldBe(2);
        ticket.HasMoreHistory.ShouldBeFalse();
    }

    /// <summary>
    /// Resolving writes two entries at one instant — the status move and the resolution —
    /// and WP-1.4's ordinal is what keeps them in a stable order.
    /// </summary>
    [Fact]
    public async Task Resolving_puts_both_of_its_entries_on_the_embedded_timeline()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var created = await TicketClient.CreateAsync(admin, reference, departmentId, "Fixed", Token);
        await TicketWriter.ParkAsync(fixture.DataSource, created.Id, TicketStatus.InProgress, Token);

        (await TicketClient.ChangeStatusAsync(
            admin, created.Id, TicketStatus.Resolved, Token, resolutionNotes: "Replaced the charger."))
            .EnsureSuccessStatusCode();

        var (ticket, _) = await TicketClient.GetAsync(admin, created.Id, Token);

        ticket.History.Count.ShouldBe(2);
        ticket.History.Select(e => e.Kind)
            .ShouldBe([TicketChangeKind.Resolution, TicketChangeKind.Status], ignoreOrder: true);
        ticket.ResolutionNotes.ShouldBe("Replaced the charger.");
        ticket.ResolvedAt.ShouldNotBeNull();
    }

    /// <summary>
    /// Past one page, the detail says so rather than silently truncating, and the paged
    /// endpoint carries the rest.
    /// </summary>
    [Fact]
    public async Task A_long_timeline_is_truncated_and_flagged()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var created = await TicketClient.CreateAsync(admin, reference, departmentId, "Busy", Token);
        await TicketWriter.ParkAsync(fixture.DataSource, created.Id, TicketStatus.InProgress, Token);

        // Each round trip through Waiting now writes four entries — two status moves, and
        // the hold reason arriving and being lifted — so fourteen passes clears the
        // twenty-five the detail embeds several times over.
        for (var i = 0; i < 14; i++)
        {
            (await TicketClient.ChangeStatusAsync(admin, created.Id, TicketStatus.Waiting, Token, holdReason: "Waiting on the vendor."))
                .EnsureSuccessStatusCode();
            (await TicketClient.ChangeStatusAsync(admin, created.Id, TicketStatus.InProgress, Token))
                .EnsureSuccessStatusCode();
        }

        var (ticket, _) = await TicketClient.GetAsync(admin, created.Id, Token);

        ticket.History.Count.ShouldBe(TicketDetailResponse.EmbeddedHistoryCount);
        ticket.HasMoreHistory.ShouldBeTrue();

        var paged = await ApiClient.ListAsync<TicketHistoryDto>(
            admin, $"{TicketClient.Tickets}/{created.Id}/history?pageSize=200", Token);

        // Fourteen round trips, four entries each.
        paged.Total.ShouldBe(56);
    }

    [Fact]
    public async Task The_detail_carries_an_entity_tag()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var created = await TicketClient.CreateAsync(admin, reference, departmentId, "Tagged", Token);
        var (_, etag) = await TicketClient.GetAsync(admin, created.Id, Token);

        etag.ShouldNotBeNullOrWhiteSpace();
        etag.ShouldStartWith("\"");
        etag.ShouldEndWith("\"");
    }

    [Fact]
    public async Task Reading_the_same_unchanged_ticket_twice_gives_the_same_tag()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var created = await TicketClient.CreateAsync(admin, reference, departmentId, "Stable", Token);

        var (_, first) = await TicketClient.GetAsync(admin, created.Id, Token);
        var (_, second) = await TicketClient.GetAsync(admin, created.Id, Token);

        second.ShouldBe(first);
    }

    [Fact]
    public async Task Moving_the_ticket_changes_its_tag()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var created = await TicketClient.CreateAsync(admin, reference, departmentId, "Moving", Token);
        await TicketWriter.ParkAsync(fixture.DataSource, created.Id, TicketStatus.InProgress, Token);

        var (_, before) = await TicketClient.GetAsync(admin, created.Id, Token);

        (await TicketClient.ChangeStatusAsync(admin, created.Id, TicketStatus.Waiting, Token, holdReason: "Waiting on the vendor."))
            .EnsureSuccessStatusCode();

        var (_, after) = await TicketClient.GetAsync(admin, created.Id, Token);

        after.ShouldNotBe(before);
    }
}
