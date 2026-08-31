using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// The ticket timeline against a real database: that a change and its history commit
/// together (invariant 3), and that the read path renders them coherently.
/// </summary>
/// <remarks>
/// <para>
/// The pure question — which entries a change owes — is exhausted in the unit suite.
/// What can only be asserted here is the transactional half: a change that was rolled
/// back must leave no line claiming it happened, and a refused one must leave none either.
/// </para>
/// <para>
/// Tickets are parked in their starting state with <c>TicketWriter.ParkAsync</c>, which
/// writes the status column directly and therefore writes no history — see the remarks
/// there, and WP-1.6, which should replace the walks that start at <c>Assigned</c> with a
/// real assignment. It is arrangement: every entry asserted below was written by the real
/// endpoint or by the real recorder.
/// </para>
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketHistoryTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>A transition through the endpoint writes one line naming the actor and both ends of the move.</summary>
    [Fact]
    public async Task A_transition_writes_a_history_entry()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var actor = (await AuthClient.ReadUserAsync(await AuthClient.MeAsync(technician, Token), Token)).Id;
        var ticket = await ParkedTicket(TicketStatus.Assigned);

        await Succeeds(technician, ticket, TicketStatus.InProgress);

        var entry = (await TicketWriter.HistoryAsync(fixture.Services, ticket, Token)).ShouldHaveSingleItem();
        entry.Kind.ShouldBe(TicketChangeKind.Status);
        entry.FromValue.ShouldBe("Assigned");
        entry.ToValue.ShouldBe("InProgress");
        entry.ActorId.ShouldBe(actor);
        entry.ActorName.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Resolving writes both lines — the status moved and the work was documented — and
    /// they carry the one instant the change happened at.
    /// </summary>
    [Fact]
    public async Task Resolving_writes_the_status_move_and_the_resolution_at_one_instant()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var ticket = await ParkedTicket(TicketStatus.InProgress);

        await Succeeds(technician, ticket, TicketStatus.Resolved, "Replaced the charger.");

        var entries = await TicketWriter.HistoryAsync(fixture.Services, ticket, Token);
        entries.Count.ShouldBe(2);

        entries.Select(entry => entry.Kind).ShouldBe([TicketChangeKind.Status, TicketChangeKind.Resolution]);
        entries[1].ToValue.ShouldBe("Replaced the charger.");

        // One instant, told apart by the ordinal — which is what keeps the pair in the same
        // order on the second read as on the first.
        entries[1].OccurredAt.ShouldBe(entries[0].OccurredAt);
        entries.Select(entry => entry.Sequence).ShouldBe([0, 1]);
    }

    /// <summary>A whole walk through the workflow reads back as a timeline with a line per move.</summary>
    /// <remarks>
    /// This is the API-level form of WP-1.4's "the detail page renders a coherent timeline":
    /// the screen is WP-1.10's, but the sequence it renders is this one, and it has to be
    /// right before there is anything to render it with.
    /// </remarks>
    [Fact]
    public async Task A_walk_through_the_workflow_reads_back_as_a_timeline()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var ticket = await ParkedTicket(TicketStatus.Assigned);

        await Succeeds(technician, ticket, TicketStatus.InProgress);
        await Succeeds(technician, ticket, TicketStatus.Waiting);
        await Succeeds(technician, ticket, TicketStatus.InProgress);
        await Succeeds(technician, ticket, TicketStatus.Resolved, "Replaced the charger.");
        await Succeeds(technician, ticket, TicketStatus.Closed);

        var page = await History(technician, ticket);

        // Newest first, so the closure is the first line a reader sees.
        page.Total.ShouldBe(6);
        page.Items.Select(entry => (entry.Kind, entry.FromValue, entry.ToValue)).ShouldBe(
        [
            (TicketChangeKind.Status, "Resolved", "Closed"),
            (TicketChangeKind.Resolution, null, "Replaced the charger."),
            (TicketChangeKind.Status, "InProgress", "Resolved"),
            (TicketChangeKind.Status, "Waiting", "InProgress"),
            (TicketChangeKind.Status, "InProgress", "Waiting"),
            (TicketChangeKind.Status, "Assigned", "InProgress"),
        ]);
    }

    /// <summary>Two reads of the same timeline come back in the same order.</summary>
    /// <remarks>
    /// The pair a resolve writes shares an instant, so the ordinal is the only thing that
    /// can order them. Without it the two lines came back either way round, which is what
    /// this asserts can no longer happen.
    /// </remarks>
    [Fact]
    public async Task Two_reads_of_a_timeline_agree_on_the_order()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var ticket = await ParkedTicket(TicketStatus.InProgress);

        await Succeeds(technician, ticket, TicketStatus.Resolved, "Replaced the charger.");

        var first = await History(technician, ticket);
        var second = await History(technician, ticket);

        first.Items.Select(entry => entry.Id).ShouldBe(second.Items.Select(entry => entry.Id));
        first.Items.Select(entry => entry.Kind).ShouldBe([TicketChangeKind.Resolution, TicketChangeKind.Status]);
    }

    /// <summary>
    /// A transaction that fails after the change and its history were saved leaves neither
    /// behind — no orphan line claiming a move that never committed.
    /// </summary>
    /// <remarks>
    /// This is WP-1.4's done-criterion, and it is why the recorder adds its entries to the
    /// caller's context instead of saving them itself. It cannot be reached through the
    /// endpoint, which opens and commits its own transaction — see
    /// <c>TicketWriter.MoveAsync</c>, which is the handler's shape with a failure point in it.
    /// </remarks>
    [Fact]
    public async Task A_rolled_back_transaction_leaves_no_orphan_history_row()
    {
        var ticket = await ParkedTicket(TicketStatus.InProgress);

        await Should.ThrowAsync<InvalidOperationException>(() => TicketWriter.MoveAsync(
            fixture.Services,
            ticket,
            TicketStatus.Resolved,
            "Replaced the charger.",
            Token,
            failAfterSave: () => throw new InvalidOperationException("The suite is rolling this back.")));

        (await TicketWriter.HistoryAsync(fixture.Services, ticket, Token)).ShouldBeEmpty();

        // And the change itself went with it, which is the other half of "in the same
        // transaction": a history that rolled back while the status stood would be the same
        // bug from the other side.
        (await TicketWriter.StateAsync(fixture.Services, ticket, Token)).Status.ShouldBe(TicketStatus.InProgress);
    }

    /// <summary>The same move committed, so the rollback test is not passing for the wrong reason.</summary>
    [Fact]
    public async Task The_same_move_without_a_failure_commits_both()
    {
        var ticket = await ParkedTicket(TicketStatus.InProgress);

        await TicketWriter.MoveAsync(fixture.Services, ticket, TicketStatus.Resolved, "Replaced the charger.", Token);

        (await TicketWriter.HistoryAsync(fixture.Services, ticket, Token)).Count.ShouldBe(2);
        (await TicketWriter.StateAsync(fixture.Services, ticket, Token)).Status.ShouldBe(TicketStatus.Resolved);
    }

    /// <summary>A transition the state machine refuses writes no line.</summary>
    [Fact]
    public async Task A_refused_transition_writes_no_history()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var ticket = await ParkedTicket(TicketStatus.New);

        (await Move(technician, ticket, TicketStatus.Closed, null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await TicketWriter.HistoryAsync(fixture.Services, ticket, Token)).ShouldBeEmpty();
    }

    /// <summary>A ticket nothing has happened to yet has an empty timeline, not a 404.</summary>
    [Fact]
    public async Task An_untouched_ticket_has_an_empty_timeline()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var ticket = await ParkedTicket(TicketStatus.New);

        var page = await History(technician, ticket);

        page.Total.ShouldBe(0);
        page.Items.ShouldBeEmpty();
    }

    /// <summary>A timeline for a ticket that does not exist is a 404, not an empty page.</summary>
    [Fact]
    public async Task A_timeline_for_no_such_ticket_is_not_found()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var response = await ApiClient.SendAsync(
            technician, HttpMethod.Get, Path(Guid.CreateVersion7()), body: null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.ticket_not_found");
    }

    /// <summary>The timeline pages, and the page is a slice of the same newest-first order.</summary>
    [Fact]
    public async Task The_timeline_pages_in_the_same_order()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var ticket = await ParkedTicket(TicketStatus.Assigned);

        await Succeeds(technician, ticket, TicketStatus.InProgress);
        await Succeeds(technician, ticket, TicketStatus.Waiting);
        await Succeeds(technician, ticket, TicketStatus.InProgress);

        var all = await History(technician, ticket);
        var first = await History(technician, ticket, page: 1, pageSize: 2);
        var second = await History(technician, ticket, page: 2, pageSize: 2);

        all.Total.ShouldBe(3);
        first.Items.Select(entry => entry.Id).ShouldBe(all.Items.Take(2).Select(entry => entry.Id));
        second.Items.Select(entry => entry.Id).ShouldBe(all.Items.Skip(2).Select(entry => entry.Id));
    }

    /// <summary>
    /// A User cannot read a timeline belonging to somebody else.
    /// </summary>
    /// <remarks>
    /// <b>WP-1.5 answered the row-level question WP-1.4 left open</b>, at the human's
    /// direction: a requester now reads their own ticket's timeline, scoped exactly as the
    /// detail endpoint is. So this is a 404 rather than WP-1.4's 403 — the tickets here are
    /// raised for a random requester id, which makes them somebody else's, and somebody
    /// else's ticket is indistinguishable from one that does not exist. The positive case —
    /// a User reading their own timeline — is in <c>TicketAccessTests</c>, alongside the
    /// rest of the row-level rule.
    /// </remarks>
    [Fact]
    public async Task A_user_cannot_read_somebody_elses_timeline()
    {
        using var user = await AuthClient.SignedInAsync(fixture, "user", Token);
        var ticket = await ParkedTicket(TicketStatus.New);

        var response = await ApiClient.SendAsync(user, HttpMethod.Get, Path(ticket), body: null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>An anonymous caller gets 401, not a redirect to a sign-in page.</summary>
    [Fact]
    public async Task An_anonymous_caller_cannot_read_a_timeline()
    {
        using var anonymous = fixture.CreateClient();
        var ticket = await ParkedTicket(TicketStatus.New);

        var response = await anonymous.GetAsync(Path(ticket), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string Path(Guid ticketId) => $"/api/v1/tickets/{ticketId}/history";

    private static Task<HttpResponseMessage> Move(
        HttpClient client,
        Guid ticketId,
        TicketStatus status,
        string? resolutionNotes) =>
        ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/tickets/{ticketId}/status-changes",
            new { status = status.ToString(), resolutionNotes },
            Token);

    private static async Task Succeeds(
        HttpClient client,
        Guid ticketId,
        TicketStatus status,
        string? resolutionNotes = null)
    {
        var response = await Move(client, ticketId, status, resolutionNotes);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<PageDto<TicketHistoryEntryDto>> History(
        HttpClient client,
        Guid ticketId,
        int? page = null,
        int? pageSize = null)
    {
        var query = page is null && pageSize is null ? string.Empty : $"?page={page}&pageSize={pageSize}";
        var response = await ApiClient.SendAsync(client, HttpMethod.Get, Path(ticketId) + query, body: null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await ApiClient.ReadAsync<PageDto<TicketHistoryEntryDto>>(response, Token);
    }

    private async Task<Guid> ParkedTicket(TicketStatus status)
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var ticket = await TicketWriter.CreateAsync(fixture.Services, TicketWriter.Draft(reference), Token);

        if (status != TicketStatus.New)
        {
            await TicketWriter.ParkAsync(fixture.DataSource, ticket.Id, status, Token);
        }

        return ticket.Id;
    }
}

/// <summary>One line of a ticket's timeline as the suite reads it off the wire.</summary>
/// <param name="Id">The entry's id.</param>
/// <param name="Kind">Which dimension moved.</param>
/// <param name="FromValue">What it read before, or null.</param>
/// <param name="ToValue">What it reads now, or null.</param>
/// <param name="OccurredAt">When the change happened.</param>
/// <param name="Sequence">Where the line sits among those the same change wrote.</param>
/// <param name="ActorId">Who made it, or null.</param>
/// <param name="ActorName">Their display name at the time, or null.</param>
public sealed record TicketHistoryEntryDto(
    Guid Id,
    TicketChangeKind Kind,
    string? FromValue,
    string? ToValue,
    DateTimeOffset OccurredAt,
    int Sequence,
    Guid? ActorId,
    string? ActorName);
