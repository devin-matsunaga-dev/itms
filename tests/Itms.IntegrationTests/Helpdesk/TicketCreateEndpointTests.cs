using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.AuditModule;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// Creation over the wire: validation, the cross-module name resolution, and the rule
/// about who may raise a ticket for whom.
/// </summary>
/// <remarks>
/// This is the first endpoint in the system that reads another module's data through
/// <c>Itms.Contracts</c>, so several of these assertions are as much about
/// <c>IUserLookup</c> and <c>IDepartmentLookup</c> actually working as about the ticket.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketCreateEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_technician_raises_a_ticket_and_it_comes_back_numbered_and_new()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        using var tech = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var ticket = await TicketClient.CreateAsync(
            tech, reference, departmentId, "Laptop will not charge", Token);

        ticket.Number.ShouldBe("TKT-0001");
        ticket.Status.ShouldBe(TicketStatus.New);
        ticket.Subject.ShouldBe("Laptop will not charge");
        ticket.AssigneeId.ShouldBeNull();
        ticket.ResolvedAt.ShouldBeNull();
        ticket.ClosedAt.ShouldBeNull();

        // WP-1.8: a ticket arrives with both clocks already running, so the due date is
        // the priority's resolution target away from the creation instant rather than
        // empty as it was through WP-1.7.
        ticket.DueAt.ShouldBe(ticket.CreatedAt.AddMinutes(reference.Targets.ResolutionMinutes));
        ticket.History.ShouldBeEmpty();
        ticket.HasMoreHistory.ShouldBeFalse();
    }

    /// <summary>
    /// The cached display names §3 rule 6 requires, resolved through the two lookup
    /// contracts rather than through a foreign key.
    /// </summary>
    [Fact]
    public async Task Creation_caches_the_requester_and_department_names_from_the_lookups()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        using var tech = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var userId = await TicketClient.UserIdAsync(fixture, "user", Token);

        var ticket = await TicketClient.CreateAsync(
            tech, reference, departmentId, "Printer jams", Token, requesterId: userId);

        ticket.RequesterId.ShouldBe(userId);
        ticket.RequesterName.ShouldNotBeNullOrWhiteSpace();
        ticket.DepartmentId.ShouldBe(departmentId);
        ticket.DepartmentName.ShouldBe("Water Operations");
    }

    /// <summary>
    /// The category and priority are Helpdesk's own rows, so their names come back live —
    /// which is what WP-1.1 meant by "renaming a category propagates by id".
    /// </summary>
    [Fact]
    public async Task Creation_returns_the_category_and_priority_as_they_read_now()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var ticket = await TicketClient.CreateAsync(
            admin, reference, departmentId, "Mailbox full", Token);

        ticket.CategoryId.ShouldBe(reference.CategoryId);
        ticket.CategoryName.ShouldNotBeNullOrWhiteSpace();
        ticket.PriorityId.ShouldBe(reference.PriorityId);
        ticket.PriorityName.ShouldNotBeNullOrWhiteSpace();
        ticket.PriorityCode.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_new_ticket_offers_the_moves_the_state_machine_allows_from_New()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var ticket = await TicketClient.CreateAsync(admin, reference, departmentId, "No signal", Token);

        ticket.AllowedNextStatuses.ShouldBe(
            TicketStateMachine.DestinationsFrom(TicketStatus.New), ignoreOrder: true);
    }

    [Fact]
    public async Task Creation_answers_201_with_a_Location_pointing_at_the_new_ticket()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var response = await TicketClient.PostAsync(
            admin, reference, departmentId, "Screen flickers", Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var ticket = await ApiClient.ReadAsync<TicketDetailDto>(response, Token);
        response.Headers.Location!.ToString().ShouldEndWith($"/api/v1/tickets/{ticket.Id}");

        // The same tag the detail endpoint would hand back, so a client can transition
        // straight away without re-reading.
        response.Headers.ETag.ShouldNotBeNull();
    }

    /// <summary>
    /// A User filing their own ticket is the commonest ticket in the system, and SPEC.md
    /// §14 gives them exactly that.
    /// </summary>
    [Fact]
    public async Task A_user_may_raise_a_ticket_for_themselves()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        using var user = await AuthClient.SignedInAsync(fixture, "user", Token);

        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var userId = await TicketClient.UserIdAsync(fixture, "user", Token);

        var ticket = await TicketClient.CreateAsync(
            user, reference, departmentId, "Cannot sign in to email", Token);

        ticket.RequesterId.ShouldBe(userId);
    }

    /// <summary>The requester defaults to the caller, so a self-service form need not send it.</summary>
    [Fact]
    public async Task Omitting_the_requester_files_the_ticket_for_the_caller()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var adminId = await TicketClient.UserIdAsync(fixture, "admin", Token);

        var ticket = await TicketClient.CreateAsync(admin, reference, departmentId, "VPN drops", Token);

        ticket.RequesterId.ShouldBe(adminId);
    }

    /// <summary>
    /// The failure case the human asked for explicitly: refused, not silently corrected.
    /// </summary>
    [Fact]
    public async Task A_user_naming_somebody_else_as_the_requester_is_refused_rather_than_corrected()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        using var user = await AuthClient.SignedInAsync(fixture, "user", Token);

        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);

        var response = await TicketClient.PostAsync(
            user, reference, departmentId, "Filed under somebody else", Token, requesterId: techId);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.requester_not_self");

        // And nothing was written: a refusal that still created the ticket under the
        // caller's own name would be the silent coercion this rule exists to prevent.
        var numbers = await TicketWriter.NumbersAsync(fixture.Services, Token);
        numbers.ShouldBeEmpty();
    }

    /// <summary>A Technician files on somebody's behalf — most of what a service desk does.</summary>
    [Fact]
    public async Task A_technician_may_raise_a_ticket_for_somebody_else()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        using var tech = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var userId = await TicketClient.UserIdAsync(fixture, "user", Token);

        var ticket = await TicketClient.CreateAsync(
            tech, reference, departmentId, "Reported by phone", Token, requesterId: userId);

        ticket.RequesterId.ShouldBe(userId);
    }

    [Theory]
    [InlineData("", "subject")]
    [InlineData("   ", "subject")]
    public async Task A_blank_subject_is_a_400_naming_the_field(string subject, string field)
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var response = await TicketClient.PostAsync(admin, reference, departmentId, subject, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Errors.ShouldNotBeNull();
        problem.Errors.ShouldContainKey(field);
    }

    [Fact]
    public async Task An_over_long_subject_is_refused_rather_than_truncated()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var response = await TicketClient.PostAsync(
            admin, reference, departmentId, new string('x', Ticket.SubjectMaxLength + 1), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_category_is_refused_and_names_the_field()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var response = await TicketClient.PostAsync(
            admin, reference, departmentId, "Unknown category", Token, categoryId: Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.category_not_found");
    }

    [Fact]
    public async Task An_unknown_priority_is_refused()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var response = await TicketClient.PostAsync(
            admin, reference, departmentId, "Unknown priority", Token, priorityId: Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.priority_not_found");
    }

    /// <summary>
    /// Retirement is WP-1.1's stand-in for deletion: existing tickets keep resolving, but
    /// nothing new may be filed against it.
    /// </summary>
    [Fact]
    public async Task A_retired_category_cannot_take_a_new_ticket()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var retire = await ApiClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"{HelpdeskClient.Categories}/{reference.CategoryId}/deactivate",
            body: null,
            Token);
        retire.EnsureSuccessStatusCode();

        var response = await TicketClient.PostAsync(
            admin, reference, departmentId, "Against a retired category", Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.category_retired");
    }

    [Fact]
    public async Task An_unknown_department_is_refused()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);

        var response = await TicketClient.PostAsync(
            admin, reference, Guid.CreateVersion7(), "Unknown department", Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.department_not_found");
    }

    /// <summary>
    /// No department given, and the requester's account names none either — which is every
    /// seeded account today, because nothing populates <c>users.department_id</c> yet.
    /// </summary>
    [Fact]
    public async Task Omitting_the_department_for_a_requester_who_has_none_is_refused()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);

        var response = await TicketClient.PostAsync(
            admin, reference, departmentId: null, "No department anywhere", Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.department_required");
    }

    [Fact]
    public async Task An_unknown_requester_is_refused()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var response = await TicketClient.PostAsync(
            admin, reference, departmentId, "Nobody", Token, requesterId: Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.requester_not_found");
    }

    /// <summary>Numbering still holds when creation goes through the endpoint.</summary>
    [Fact]
    public async Task Successive_creations_are_numbered_in_sequence_with_no_gaps()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        for (var i = 0; i < 3; i++)
        {
            await TicketClient.CreateAsync(admin, reference, departmentId, $"Ticket {i}", Token);
        }

        var numbers = await TicketWriter.NumbersAsync(fixture.Services, Token);
        numbers.ShouldBe(["TKT-0001", "TKT-0002", "TKT-0003"]);
    }

    /// <summary>
    /// A refused creation must not burn a number — that is what WP-1.2 chose a counter row
    /// over a PostgreSQL sequence for, and the endpoint is the first thing that can prove it.
    /// </summary>
    [Fact]
    public async Task A_refused_creation_leaves_the_numbering_untouched()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        await TicketClient.CreateAsync(admin, reference, departmentId, "First", Token);

        var refused = await TicketClient.PostAsync(
            admin, reference, departmentId, "Doomed", Token, priorityId: Guid.CreateVersion7());
        refused.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var next = await TicketClient.CreateAsync(admin, reference, departmentId, "Second", Token);
        next.Number.ShouldBe("TKT-0002");
    }

    /// <summary>
    /// SPEC.md §15 counts ticket modifications as mandatory audit coverage, and coverage
    /// that cannot say who acted is not coverage.
    /// </summary>
    /// <remarks>
    /// WP-1.6 stamped <c>ActorId</c> onto every event its two handlers publish and
    /// recorded that <c>CreateTicketHandler</c> had been missed, so every creation since
    /// WP-1.5 was audited as having happened at nobody's hand. This is the assertion that
    /// would have failed. The row is written by the Audit module's consumer, so it arrives
    /// with the dispatcher rather than inside the request.
    /// </remarks>
    [Fact]
    public async Task Creation_is_audited_against_the_account_that_raised_the_ticket()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        using var tech = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);

        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var ticket = await TicketClient.CreateAsync(
            tech, reference, departmentId, "Monitor flickers", Token);

        await Eventually.UntilAsync(
            async () => (await AuditQueries
                .ByEntityAsync(fixture.DataSource, "Ticket", ticket.Id.ToString(), Token)).Count >= 1,
            $"an audit row for ticket {ticket.Id}",
            Token);

        var rows = await AuditQueries.ByEntityAsync(fixture.DataSource, "Ticket", ticket.Id.ToString(), Token);

        rows.Single(row => row.Action == "ticket.created").ActorId.ShouldBe(techId);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_raise_a_ticket()
    {
        using var anonymous = fixture.CreateClient();
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);

        var response = await TicketClient.PostAsync(
            anonymous, reference, Guid.CreateVersion7(), "Anonymous", Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
