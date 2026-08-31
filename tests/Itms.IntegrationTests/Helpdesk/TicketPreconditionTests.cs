using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// The ETag and <c>If-Match</c> surface ARCHITECTURE.md §6 asks for.
/// </summary>
/// <remarks>
/// WP-1.2 mapped the <c>xmin</c> token and WP-1.3 turned the exception it raises into a
/// 409. What was missing was the half that lets a client find out <em>before</em> it has
/// done the work: a 412 is the ticket refusing a request whose stated precondition no
/// longer holds, and nothing was attempted.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketPreconditionTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_matching_If_Match_lets_the_transition_through()
    {
        var (admin, ticketId) = await InProgressTicketAsync();
        var (_, etag) = await TicketClient.GetAsync(admin, ticketId, Token);

        var response = await TicketClient.ChangeStatusAsync(
            admin, ticketId, TicketStatus.Waiting, Token, ifMatch: etag);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var state = await TicketWriter.StateAsync(fixture.Services, ticketId, Token);
        state.Status.ShouldBe(TicketStatus.Waiting);
    }

    [Fact]
    public async Task A_stale_If_Match_is_refused_with_412_and_moves_nothing()
    {
        var (admin, ticketId) = await InProgressTicketAsync();
        var (_, stale) = await TicketClient.GetAsync(admin, ticketId, Token);

        // Somebody else moves it in between, exactly as a second technician would.
        (await TicketClient.ChangeStatusAsync(admin, ticketId, TicketStatus.Waiting, Token))
            .EnsureSuccessStatusCode();

        var response = await TicketClient.ChangeStatusAsync(
            admin, ticketId, TicketStatus.InProgress, Token, ifMatch: stale);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.ticket_conflict");
        problem.Status.ShouldBe(412);

        // Nothing was attempted: the ticket is still where the other request left it.
        var state = await TicketWriter.StateAsync(fixture.Services, ticketId, Token);
        state.Status.ShouldBe(TicketStatus.Waiting);
    }

    /// <summary>
    /// The refusal must leave no trace — no history entry, in particular, or invariant 3's
    /// timeline would carry a move that never happened.
    /// </summary>
    [Fact]
    public async Task A_refused_precondition_writes_no_history()
    {
        var (admin, ticketId) = await InProgressTicketAsync();
        var (_, stale) = await TicketClient.GetAsync(admin, ticketId, Token);

        (await TicketClient.ChangeStatusAsync(admin, ticketId, TicketStatus.Waiting, Token))
            .EnsureSuccessStatusCode();

        var before = await TicketWriter.HistoryAsync(fixture.Services, ticketId, Token);

        var response = await TicketClient.ChangeStatusAsync(
            admin, ticketId, TicketStatus.InProgress, Token, ifMatch: stale);
        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);

        var after = await TicketWriter.HistoryAsync(fixture.Services, ticketId, Token);
        after.Count.ShouldBe(before.Count);
    }

    /// <summary>WP-1.3's behaviour, unchanged: no header means no precondition.</summary>
    [Fact]
    public async Task Sending_no_If_Match_proceeds_exactly_as_before()
    {
        var (admin, ticketId) = await InProgressTicketAsync();

        var response = await TicketClient.ChangeStatusAsync(admin, ticketId, TicketStatus.Waiting, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>RFC 9110: <c>*</c> matches any existing representation, and the row exists.</summary>
    [Fact]
    public async Task A_wildcard_If_Match_matches_any_ticket_that_exists()
    {
        var (admin, ticketId) = await InProgressTicketAsync();

        var response = await TicketClient.ChangeStatusAsync(
            admin, ticketId, TicketStatus.Waiting, Token, ifMatch: "*");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// A tag this endpoint could never have issued cannot match, and RFC 9110 §13.1.1 makes
    /// that a failed precondition rather than a bad request.
    /// </summary>
    [Fact]
    public async Task A_malformed_If_Match_fails_the_precondition_rather_than_being_ignored()
    {
        var (admin, ticketId) = await InProgressTicketAsync();

        var response = await TicketClient.ChangeStatusAsync(
            admin, ticketId, TicketStatus.Waiting, Token, ifMatch: "\"not-a-version\"");

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);

        var state = await TicketWriter.StateAsync(fixture.Services, ticketId, Token);
        state.Status.ShouldBe(TicketStatus.InProgress);
    }

    /// <summary>A list of tags matches when any one of them does.</summary>
    [Fact]
    public async Task An_If_Match_list_containing_the_current_tag_matches()
    {
        var (admin, ticketId) = await InProgressTicketAsync();
        var (_, etag) = await TicketClient.GetAsync(admin, ticketId, Token);

        var response = await TicketClient.ChangeStatusAsync(
            admin, ticketId, TicketStatus.Waiting, Token, ifMatch: $"\"4294967295\", {etag}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// The precondition is checked after the ticket is found, so a nonexistent ticket is
    /// still a 404 — the caller's problem is the id, not the version.
    /// </summary>
    [Fact]
    public async Task A_precondition_on_a_ticket_that_does_not_exist_is_still_a_404()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);

        var response = await TicketClient.ChangeStatusAsync(
            admin, Guid.CreateVersion7(), TicketStatus.Waiting, Token, ifMatch: "\"1\"");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>The tag the create response hands back is immediately usable.</summary>
    [Fact]
    public async Task The_tag_from_creation_works_as_a_precondition_without_re_reading()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var response = await TicketClient.PostAsync(admin, reference, departmentId, "Fresh", Token);
        response.EnsureSuccessStatusCode();

        var ticket = await ApiClient.ReadAsync<TicketDetailDto>(response, Token);
        var etag = response.Headers.ETag!.ToString();

        await TicketWriter.ParkAsync(fixture.DataSource, ticket.Id, TicketStatus.InProgress, Token);

        // Parking wrote the row directly, so the creation tag is now genuinely stale — which
        // is the point: the precondition notices a change this client never made.
        var moved = await TicketClient.ChangeStatusAsync(
            admin, ticket.Id, TicketStatus.Waiting, Token, ifMatch: etag);

        moved.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
    }

    private async Task<(HttpClient Admin, Guid TicketId)> InProgressTicketAsync()
    {
        var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var created = await TicketClient.CreateAsync(admin, reference, departmentId, "Working", Token);
        await TicketWriter.ParkAsync(fixture.DataSource, created.Id, TicketStatus.InProgress, Token);

        return (admin, created.Id);
    }
}
