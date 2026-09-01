using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// Putting a ticket on hold over the wire: the reason is required, it reaches the detail,
/// and it reaches the timeline beside the status move.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketHoldEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private const string HoldReason = "Waiting on the vendor to ship a replacement roller.";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Holding_without_a_reason_is_refused_before_the_ticket_moves()
    {
        var world = await WorldAsync();

        var response = await TicketClient.ChangeStatusAsync(
            world.Admin, world.TicketId, TicketStatus.Waiting, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Errors.ShouldNotBeNull().ShouldContainKey("holdReason");

        var (ticket, _) = await TicketClient.GetAsync(world.Admin, world.TicketId, Token);
        ticket.Status.ShouldBe(TicketStatus.InProgress);
    }

    [Fact]
    public async Task Holding_with_a_reason_parks_the_ticket_and_keeps_the_reason_on_it()
    {
        var world = await WorldAsync();

        var response = await TicketClient.ChangeStatusAsync(
            world.Admin, world.TicketId, TicketStatus.Waiting, Token, holdReason: HoldReason);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (ticket, _) = await TicketClient.GetAsync(world.Admin, world.TicketId, Token);
        ticket.Status.ShouldBe(TicketStatus.Waiting);
        ticket.HoldReason.ShouldBe(HoldReason);
    }

    [Fact]
    public async Task A_reason_offered_to_another_destination_is_refused()
    {
        // The mirror of resolution notes, so text somebody typed is never silently dropped.
        var world = await WorldAsync();

        var response = await TicketClient.ChangeStatusAsync(
            world.Admin, world.TicketId, TicketStatus.Cancelled, Token, holdReason: HoldReason);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_hold_and_its_reason_arrive_in_the_timeline_as_one_event()
    {
        // Two entries at one instant, exactly as resolving writes two — which is what lets
        // the detail screen render "on hold, because X" as a single row.
        var world = await WorldAsync();

        (await TicketClient.ChangeStatusAsync(
            world.Admin, world.TicketId, TicketStatus.Waiting, Token, holdReason: HoldReason))
            .EnsureSuccessStatusCode();

        var (ticket, _) = await TicketClient.GetAsync(world.Admin, world.TicketId, Token);

        var hold = ticket.History.Single(entry => entry.Kind == TicketChangeKind.Hold);
        var status = ticket.History.First(entry => entry.Kind == TicketChangeKind.Status);

        hold.ToValue.ShouldBe(HoldReason);
        hold.OccurredAt.ShouldBe(status.OccurredAt);
    }

    [Fact]
    public async Task Resuming_clears_the_reason_and_records_that_it_was_lifted()
    {
        var world = await WorldAsync();

        (await TicketClient.ChangeStatusAsync(
            world.Admin, world.TicketId, TicketStatus.Waiting, Token, holdReason: HoldReason))
            .EnsureSuccessStatusCode();
        (await TicketClient.ChangeStatusAsync(
            world.Admin, world.TicketId, TicketStatus.InProgress, Token))
            .EnsureSuccessStatusCode();

        var (ticket, _) = await TicketClient.GetAsync(world.Admin, world.TicketId, Token);

        ticket.Status.ShouldBe(TicketStatus.InProgress);
        ticket.HoldReason.ShouldBeNull();
        ticket.History.Count(entry => entry.Kind == TicketChangeKind.Hold).ShouldBe(2);
    }

    [Fact]
    public async Task Holding_a_second_time_for_the_same_reason_is_recorded_again()
    {
        // The reason clearing on resume is what makes this true; without it the second
        // hold would be no diff at all and the timeline would claim it never happened.
        var world = await WorldAsync();

        foreach (var _ in Enumerable.Range(0, 2))
        {
            (await TicketClient.ChangeStatusAsync(
                world.Admin, world.TicketId, TicketStatus.Waiting, Token, holdReason: HoldReason))
                .EnsureSuccessStatusCode();
            (await TicketClient.ChangeStatusAsync(
                world.Admin, world.TicketId, TicketStatus.InProgress, Token))
                .EnsureSuccessStatusCode();
        }

        var (ticket, _) = await TicketClient.GetAsync(world.Admin, world.TicketId, Token);

        // Two holds and two lifts.
        ticket.History.Count(entry => entry.Kind == TicketChangeKind.Hold).ShouldBe(4);
    }

    [Fact]
    public async Task The_requester_sees_why_their_own_ticket_is_waiting()
    {
        // WP-1.5 widened the timeline to the requester, and this is the kind of thing it
        // was widened for: "waiting on the vendor" is what they most want to know.
        var world = await WorldAsync(forTheEndUser: true);
        (await TicketClient.ChangeStatusAsync(
            world.Admin, world.TicketId, TicketStatus.Waiting, Token, holdReason: HoldReason))
            .EnsureSuccessStatusCode();

        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);
        var (ticket, _) = await TicketClient.GetAsync(endUser, world.TicketId, Token);

        ticket.HoldReason.ShouldBe(HoldReason);
        ticket.History.ShouldContain(entry => entry.Kind == TicketChangeKind.Hold);
    }

    private async Task<World> WorldAsync(bool forTheEndUser = false)
    {
        var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var department = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var requesterId = forTheEndUser
            ? await TicketClient.UserIdAsync(fixture, "user", Token)
            : (Guid?)null;

        var ticket = await TicketClient.CreateAsync(
            admin, reference, department, "Printer jammed", Token, requesterId: requesterId);

        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);
        (await TicketClient.AssignAsync(admin, ticket.Id, techId, Token)).EnsureSuccessStatusCode();
        (await TicketClient.ChangeStatusAsync(admin, ticket.Id, TicketStatus.InProgress, Token))
            .EnsureSuccessStatusCode();

        return new World(admin, ticket.Id);
    }

    private sealed record World(HttpClient Admin, Guid TicketId);
}
