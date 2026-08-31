using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.AuditModule;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// The state machine over the wire.
/// </summary>
/// <remarks>
/// <para>
/// WP-1.3's second done-criterion is that "the API rejects illegal ones even when the
/// client sends them directly", which is the half a unit test cannot reach: there is no
/// interface here to hide a button in, so every request below is the hand-crafted one the
/// criterion is about.
/// </para>
/// <para>
/// Tickets are parked in their starting state with <c>TicketWriter.ParkAsync</c> — see the
/// remarks there for why, and for what WP-1.6 should change about it. Every transition
/// under test still goes through the real endpoint.
/// </para>
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketStatusEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public static TheoryData<TicketStatus, TicketStatus> LegalTransitions()
    {
        var data = new TheoryData<TicketStatus, TicketStatus>();

        foreach (var from in Enum.GetValues<TicketStatus>())
        {
            foreach (var to in TicketStateMachine.DestinationsFrom(from))
            {
                // Assigned is legal in the machine but not offered by this endpoint —
                // it has no assignee to set. See TicketEndpoints and WP-1.6.
                if (to != TicketStatus.Assigned)
                {
                    data.Add(from, to);
                }
            }
        }

        return data;
    }

    public static TheoryData<TicketStatus, TicketStatus> IllegalTransitions()
    {
        var data = new TheoryData<TicketStatus, TicketStatus>();

        foreach (var from in Enum.GetValues<TicketStatus>())
        {
            foreach (var to in Enum.GetValues<TicketStatus>())
            {
                if (!TicketStateMachine.CanTransition(from, to) && to != TicketStatus.Assigned)
                {
                    data.Add(from, to);
                }
            }
        }

        return data;
    }

    /// <summary>Every legal move this endpoint offers is accepted and persisted.</summary>
    [Theory]
    [MemberData(nameof(LegalTransitions))]
    public async Task A_legal_transition_is_accepted_and_persisted(TicketStatus from, TicketStatus to)
    {
        using var technician = await SignedInAsync("tech");
        var ticket = await ParkedTicket(from);

        var response = await Move(technician, ticket, to, to == TicketStatus.Resolved ? "Swapped the unit." : null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ApiClient.ReadAsync<TicketStatusChangeDto>(response, Token);
        body.PreviousStatus.ShouldBe(from);
        body.Status.ShouldBe(to);

        (await TicketWriter.StateAsync(fixture.Services, ticket, Token)).Status.ShouldBe(to);
    }

    /// <summary>
    /// Every illegal move is refused with a 409, and — the assertion that matters — the
    /// row does not move. Invariant 2.
    /// </summary>
    [Theory]
    [MemberData(nameof(IllegalTransitions))]
    public async Task An_illegal_transition_is_refused_with_409_and_moves_nothing(TicketStatus from, TicketStatus to)
    {
        using var technician = await SignedInAsync("tech");
        var ticket = await ParkedTicket(from);

        var response = await Move(technician, ticket, to, to == TicketStatus.Resolved ? "Swapped the unit." : null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.illegal_transition");

        (await TicketWriter.StateAsync(fixture.Services, ticket, Token)).Status.ShouldBe(from);
    }

    /// <summary>The whole happy path SPEC.md §2 draws, walked one request at a time.</summary>
    [Fact]
    public async Task The_workflow_walks_from_Assigned_to_Closed()
    {
        using var technician = await SignedInAsync("tech");
        var ticket = await ParkedTicket(TicketStatus.Assigned);

        await Succeeds(technician, ticket, TicketStatus.InProgress);
        await Succeeds(technician, ticket, TicketStatus.Waiting);
        await Succeeds(technician, ticket, TicketStatus.InProgress);
        await Succeeds(technician, ticket, TicketStatus.Resolved, "Replaced the charger.");
        var closed = await Succeeds(technician, ticket, TicketStatus.Closed);

        closed.Status.ShouldBe(TicketStatus.Closed);
        closed.ClosedAt.ShouldNotBeNull();
        closed.AllowedNextStatuses.ShouldBeEmpty();

        var state = await TicketWriter.StateAsync(fixture.Services, ticket, Token);
        state.Status.ShouldBe(TicketStatus.Closed);
        state.ResolvedAt.ShouldNotBeNull();
        state.ClosedAt.ShouldNotBeNull();
        state.Notes.ShouldBe("Replaced the charger.");
    }

    [Fact]
    public async Task Resolving_records_the_notes_and_the_instant()
    {
        using var technician = await SignedInAsync("tech");
        var ticket = await ParkedTicket(TicketStatus.InProgress);

        var body = await Succeeds(technician, ticket, TicketStatus.Resolved, "  Replaced the charger.  ");

        body.ResolvedAt.ShouldNotBeNull();
        body.ClosedAt.ShouldBeNull();
        (await TicketWriter.StateAsync(fixture.Services, ticket, Token)).Notes.ShouldBe("Replaced the charger.");
    }

    [Fact]
    public async Task Reopening_a_resolved_ticket_clears_the_resolved_instant_and_keeps_the_notes()
    {
        using var technician = await SignedInAsync("tech");
        var ticket = await ParkedTicket(TicketStatus.InProgress);

        await Succeeds(technician, ticket, TicketStatus.Resolved, "Replaced the charger.");
        var reopened = await Succeeds(technician, ticket, TicketStatus.InProgress);

        reopened.PreviousStatus.ShouldBe(TicketStatus.Resolved);
        reopened.Status.ShouldBe(TicketStatus.InProgress);
        reopened.ResolvedAt.ShouldBeNull();

        var state = await TicketWriter.StateAsync(fixture.Services, ticket, Token);
        state.ResolvedAt.ShouldBeNull();
        state.Notes.ShouldBe("Replaced the charger.");
    }

    [Fact]
    public async Task The_response_offers_exactly_the_destinations_the_machine_allows()
    {
        using var technician = await SignedInAsync("tech");
        var ticket = await ParkedTicket(TicketStatus.Assigned);

        var body = await Succeeds(technician, ticket, TicketStatus.InProgress);

        body.AllowedNextStatuses.Order().ShouldBe(
            TicketStateMachine.DestinationsFrom(TicketStatus.InProgress).Order());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Resolving_without_notes_is_a_400_naming_the_field(string? notes)
    {
        using var technician = await SignedInAsync("tech");
        var ticket = await ParkedTicket(TicketStatus.InProgress);

        var response = await Move(technician, ticket, TicketStatus.Resolved, notes);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Errors.ShouldNotBeNull().ShouldContainKey("resolutionNotes");

        (await TicketWriter.StateAsync(fixture.Services, ticket, Token)).Status.ShouldBe(TicketStatus.InProgress);
    }

    [Fact]
    public async Task Notes_on_a_transition_that_does_not_resolve_are_refused()
    {
        using var technician = await SignedInAsync("tech");
        var ticket = await ParkedTicket(TicketStatus.InProgress);

        var response = await Move(technician, ticket, TicketStatus.Waiting, "Not a resolution.");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token))
            .Errors.ShouldNotBeNull().ShouldContainKey("resolutionNotes");
    }

    [Fact]
    public async Task Notes_longer_than_the_column_are_refused()
    {
        using var technician = await SignedInAsync("tech");
        var ticket = await ParkedTicket(TicketStatus.InProgress);

        var response = await Move(
            technician, ticket, TicketStatus.Resolved, new string('x', Ticket.ResolutionNotesMaxLength + 1));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Assigned is a legal state, but this endpoint cannot produce it: it carries no
    /// assignee, and a ticket assigned to nobody would be a lie. WP-1.6 owns the move.
    /// </summary>
    [Fact]
    public async Task Moving_to_Assigned_is_refused_because_assignment_is_what_does_it()
    {
        using var technician = await SignedInAsync("tech");
        var ticket = await ParkedTicket(TicketStatus.New);

        var response = await Move(technician, ticket, TicketStatus.Assigned, null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token))
            .Errors.ShouldNotBeNull().ShouldContainKey("status");

        (await TicketWriter.StateAsync(fixture.Services, ticket, Token)).Status.ShouldBe(TicketStatus.New);
    }

    /// <summary>
    /// A status the enum does not have never reaches the handler — and comes back as a
    /// 400 ProblemDetails rather than the 500 model binding used to produce. See
    /// <c>MalformedRequestExceptionHandler</c>: the gap was repo-wide, not this
    /// endpoint's, and this is the test that fails without the fix.
    /// </summary>
    [Fact]
    public async Task A_status_that_is_not_a_status_is_refused()
    {
        using var technician = await SignedInAsync("tech");
        var ticket = await ParkedTicket(TicketStatus.New);

        var response = await ApiClient.SendAsync(
            technician,
            HttpMethod.Post,
            Path(ticket),
            new { status = "Escalated", resolutionNotes = (string?)null },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("request.malformed");
        (await TicketWriter.StateAsync(fixture.Services, ticket, Token)).Status.ShouldBe(TicketStatus.New);
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task A_terminal_ticket_refuses_every_move(TicketStatus terminal)
    {
        using var technician = await SignedInAsync("tech");
        var ticket = await ParkedTicket(terminal);

        foreach (var target in Enum.GetValues<TicketStatus>().Where(s => s != TicketStatus.Assigned))
        {
            var response = await Move(
                technician, ticket, target, target == TicketStatus.Resolved ? "Swapped the unit." : null);

            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        (await TicketWriter.StateAsync(fixture.Services, ticket, Token)).Status.ShouldBe(terminal);
    }

    [Fact]
    public async Task An_unknown_ticket_is_a_404()
    {
        using var technician = await SignedInAsync("tech");

        var response = await ApiClient.SendAsync(
            technician,
            HttpMethod.Post,
            $"/api/v1/tickets/{Guid.CreateVersion7()}/status-changes",
            new { status = "Cancelled", resolutionNotes = (string?)null },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.ticket_not_found");
    }

    /// <summary>
    /// The soft-delete filter WP-1.2 put in place ahead of any delete path: a deleted
    /// ticket is not there to be transitioned.
    /// </summary>
    [Fact]
    public async Task A_soft_deleted_ticket_is_a_404()
    {
        using var technician = await SignedInAsync("tech");
        var ticket = await ParkedTicket(TicketStatus.New);

        await using (var connection = await fixture.DataSource.OpenConnectionAsync(Token))
        {
            await using var command = new Npgsql.NpgsqlCommand(
                "UPDATE helpdesk.tickets SET deleted_at = now() AT TIME ZONE 'utc' WHERE id = @id", connection);
            command.Parameters.AddWithValue("id", ticket);
            await command.ExecuteNonQueryAsync(Token);
        }

        var response = await Move(technician, ticket, TicketStatus.Cancelled, null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_anonymous_caller_is_refused()
    {
        using var client = fixture.CreateClient();
        var ticket = await ParkedTicket(TicketStatus.New);

        var response = await ApiClient.SendAsync(
            client, HttpMethod.Post, Path(ticket), new { status = "Cancelled", resolutionNotes = (string?)null }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await TicketWriter.StateAsync(fixture.Services, ticket, Token)).Status.ShouldBe(TicketStatus.New);
    }

    /// <summary>
    /// ARCHITECTURE.md §7: a User may read and comment on their own tickets "and nothing
    /// else". Cancelling their own ticket is not on that list, and the server is what says
    /// so — not the absence of a button.
    /// </summary>
    [Fact]
    public async Task An_end_user_cannot_move_a_ticket()
    {
        using var user = await SignedInAsync("user");
        var ticket = await ParkedTicket(TicketStatus.New);

        var response = await Move(user, ticket, TicketStatus.Cancelled, null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await TicketWriter.StateAsync(fixture.Services, ticket, Token)).Status.ShouldBe(TicketStatus.New);
    }

    [Fact]
    public async Task An_admin_can_move_a_ticket()
    {
        using var admin = await SignedInAsync("admin");
        var ticket = await ParkedTicket(TicketStatus.New);

        (await Move(admin, ticket, TicketStatus.Cancelled, null)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_request_without_an_antiforgery_token_is_refused()
    {
        using var technician = await SignedInAsync("tech");
        var ticket = await ParkedTicket(TicketStatus.New);

        using var request = new HttpRequestMessage(HttpMethod.Post, Path(ticket))
        {
            Content = System.Net.Http.Json.JsonContent.Create(
                new { status = "Cancelled", resolutionNotes = (string?)null }),
        };
        var response = await technician.SendAsync(request, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("auth.antiforgery_failed");
        (await TicketWriter.StateAsync(fixture.Services, ticket, Token)).Status.ShouldBe(TicketStatus.New);
    }

    /// <summary>
    /// SPEC.md §15 counts ticket modifications as mandatory audit coverage, and a status
    /// transition is the modification the workflow is made of.
    /// </summary>
    [Fact]
    public async Task A_transition_writes_an_audit_row_naming_the_actor_and_the_move()
    {
        var client = fixture.CreateClient();
        using var technician = client;
        var login = await AuthClient.LoginAsync(client, "tech", AuthClient.Password, Token);
        login.EnsureSuccessStatusCode();
        var actor = (await AuthClient.ReadUserAsync(login, Token)).Id;

        var ticket = await ParkedTicket(TicketStatus.InProgress);
        await Succeeds(technician, ticket, TicketStatus.Resolved, "Replaced the charger.");

        var row = (await AuditQueries.ByEntityAsync(fixture.DataSource, "Ticket", ticket.ToString(), Token))
            .ShouldHaveSingleItem();

        row.Action.ShouldBe("helpdesk.ticket_status_changed");
        row.ActorId.ShouldBe(actor);
        row.SourceIp.ShouldBe(IdentityWebFixture.RemoteIpAddress);
        row.Changes["status"].ShouldBe(new("InProgress", "Resolved"));
        row.Changes["resolutionNotes"].ShouldBe(new(null, "Replaced the charger."));
        row.Changes.ShouldContainKey("resolvedAt");
        row.Changes.ShouldNotContainKey("closedAt");
    }

    /// <summary>
    /// The audit row and the change commit together, so a refused transition must leave
    /// no entry claiming it happened.
    /// </summary>
    [Fact]
    public async Task A_refused_transition_writes_no_audit_row()
    {
        using var technician = await SignedInAsync("tech");
        var ticket = await ParkedTicket(TicketStatus.New);

        (await Move(technician, ticket, TicketStatus.Closed, null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await AuditQueries.ByEntityAsync(fixture.DataSource, "Ticket", ticket.ToString(), Token)).ShouldBeEmpty();
    }

    private static string Path(Guid ticketId) => $"/api/v1/tickets/{ticketId}/status-changes";

    private static Task<HttpResponseMessage> Move(
        HttpClient client,
        Guid ticketId,
        TicketStatus status,
        string? resolutionNotes) =>
        ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            Path(ticketId),
            new { status = status.ToString(), resolutionNotes },
            Token);

    private static async Task<TicketStatusChangeDto> Succeeds(
        HttpClient client,
        Guid ticketId,
        TicketStatus status,
        string? resolutionNotes = null)
    {
        var response = await Move(client, ticketId, status, resolutionNotes);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await ApiClient.ReadAsync<TicketStatusChangeDto>(response, Token);
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

    private async Task<HttpClient> SignedInAsync(string userName)
    {
        var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        return client;
    }
}

/// <summary>A status change as the suite reads it off the wire.</summary>
/// <param name="Id">The ticket.</param>
/// <param name="Number">Its human-readable number.</param>
/// <param name="PreviousStatus">Where it was.</param>
/// <param name="Status">Where it is now.</param>
/// <param name="ChangedAt">When the move happened.</param>
/// <param name="ResolvedAt">When it was resolved, or null.</param>
/// <param name="ClosedAt">When it was closed, or null.</param>
/// <param name="AllowedNextStatuses">Where it may go next.</param>
public sealed record TicketStatusChangeDto(
    Guid Id,
    string Number,
    TicketStatus PreviousStatus,
    TicketStatus Status,
    DateTimeOffset ChangedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<TicketStatus> AllowedNextStatuses);
