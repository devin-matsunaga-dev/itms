using System.Net;
using System.Net.Http.Json;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.AuditModule;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// The comment routes over the wire: who may post, what comes back, and what the trail
/// records.
/// </summary>
/// <remarks>
/// The audience rule has a suite of its own — <see cref="TicketInternalVisibilityTests"/>
/// is where WP-1.7's done-criteria live. This file covers everything else the endpoints
/// promise.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketCommentEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_technician_posts_a_comment_and_it_comes_back_attributed_and_timestamped()
    {
        var ticket = await TicketAsync();
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var comment = await TicketThreadClient.CommentsAsync(technician, ticket, "Ordered a charger.", Token);

        comment.Id.ShouldNotBe(Guid.Empty);
        comment.TicketId.ShouldBe(ticket);
        comment.Body.ShouldBe("Ordered a charger.");
        comment.IsInternal.ShouldBeFalse();
        comment.AuthorId.ShouldBe(techId);
        comment.AuthorName.ShouldNotBeNullOrWhiteSpace();
        comment.CreatedAt.ShouldNotBe(default);
    }

    /// <summary>
    /// Created, not Ok: a comment is a new resource and the location header names it, the
    /// same shape the create endpoint uses.
    /// </summary>
    [Fact]
    public async Task Posting_answers_201_with_a_location()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var response = await TicketThreadClient.PostCommentAsync(technician, ticket, "Noted.", Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.ToString().ShouldStartWith($"/api/v1/tickets/{ticket}/comments/");
    }

    /// <summary>
    /// SPEC.md §14 gives a User the right to comment on their own ticket. This is the
    /// positive half of the rule the visibility suite asserts the negative half of.
    /// </summary>
    [Fact]
    public async Task A_requester_may_comment_on_their_own_ticket()
    {
        var ticket = await OwnTicketAsync();
        var endUserId = await TicketClient.UserIdAsync(fixture, "user", Token);

        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var comment = await TicketThreadClient.CommentsAsync(endUser, ticket, "It is still not charging.", Token);

        comment.AuthorId.ShouldBe(endUserId);
        comment.IsInternal.ShouldBeFalse();
    }

    /// <summary>
    /// Somebody else's ticket answers 404 on this route as on every other, taking
    /// ARCHITECTURE.md §6's enumeration exception — a 403 here would confirm the ticket
    /// exists.
    /// </summary>
    [Fact]
    public async Task Commenting_on_somebody_elses_ticket_is_not_found()
    {
        var ticket = await TicketAsync();

        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var response = await TicketThreadClient.PostCommentAsync(endUser, ticket, "Not mine.", Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.ticket_not_found");
    }

    /// <summary>
    /// A ticket that never existed and one the caller may not see have to be the same
    /// answer — the assertion WP-1.5 made for the detail read, made again here, because a
    /// difference in the message would reopen the leak without failing an obvious test.
    /// </summary>
    /// <remarks>
    /// Status, code, and detail rather than the raw body: the problem document carries a
    /// per-request <c>traceId</c>, so two responses that are correctly indistinguishable
    /// are never textually equal. Those three fields are everything a caller could tell
    /// them apart by.
    /// </remarks>
    [Fact]
    public async Task A_forbidden_ticket_and_a_nonexistent_one_answer_identically()
    {
        var ticket = await TicketAsync();

        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var forbidden = await TicketThreadClient.PostCommentAsync(endUser, ticket, "Not mine.", Token);
        var missing = await TicketThreadClient.PostCommentAsync(
            endUser, Guid.CreateVersion7(), "Not mine.", Token);

        forbidden.StatusCode.ShouldBe(missing.StatusCode);

        var one = await ApiClient.ReadAsync<ProblemDto>(forbidden, Token);
        var other = await ApiClient.ReadAsync<ProblemDto>(missing, Token);

        one.Code.ShouldBe(other.Code);
        one.Detail.ShouldBe(other.Detail);
        one.Title.ShouldBe(other.Title);
    }

    [Fact]
    public async Task The_thread_comes_back_newest_first()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        await TicketThreadClient.CommentsAsync(technician, ticket, "First", Token);
        await TicketThreadClient.CommentsAsync(technician, ticket, "Second", Token);
        await TicketThreadClient.CommentsAsync(technician, ticket, "Third", Token);

        var page = await TicketThreadClient.ListCommentsAsync(technician, ticket, Token);

        page.Items.Select(comment => comment.Body).ShouldBe(["Third", "Second", "First"]);
    }

    /// <summary>
    /// A ticket nobody has commented on is an empty page, not a 404 — the same distinction
    /// the timeline draws, and the reason the existence check is made against the ticket
    /// rather than against the comments.
    /// </summary>
    [Fact]
    public async Task A_ticket_with_no_comments_is_an_empty_page_not_a_missing_one()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var page = await TicketThreadClient.ListCommentsAsync(technician, ticket, Token);

        page.Items.ShouldBeEmpty();
        page.Total.ShouldBe(0);
    }

    [Fact]
    public async Task Reading_the_thread_of_a_ticket_that_does_not_exist_is_not_found()
    {
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var response = await technician.GetAsync(
            new Uri($"/api/v1/tickets/{Guid.CreateVersion7()}/comments", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_comment_is_refused(string body)
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var response = await TicketThreadClient.PostCommentAsync(technician, ticket, body, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Errors!.ShouldContainKey("body");
    }

    [Fact]
    public async Task A_comment_longer_than_the_column_is_refused_by_the_validator()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var response = await TicketThreadClient.PostCommentAsync(
            technician, ticket, new string('x', TicketComment.BodyMaxLength + 1), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// SPEC.md §2 restricts transitions and says nothing about the conversation. A
    /// requester replying to a resolution is how a ticket gets reopened, so refusing here
    /// would have been a rule this package invented.
    /// </summary>
    [Fact]
    public async Task A_closed_ticket_still_accepts_a_comment()
    {
        var ticket = await TicketAsync();

        await TicketWriter.ParkAsync(fixture.DataSource, ticket, TicketStatus.Closed, Token);

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var comment = await TicketThreadClient.CommentsAsync(technician, ticket, "For the record.", Token);

        comment.Body.ShouldBe("For the record.");
    }

    /// <summary>
    /// Cookie auth plus a state-changing verb is the shape CSRF exploits, and
    /// CONVENTIONS.md's security floor requires the check on every one of them.
    /// </summary>
    [Fact]
    public async Task A_post_without_an_antiforgery_token_is_refused()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var response = await technician.PostAsJsonAsync(
            new Uri($"/api/v1/tickets/{ticket}/comments", UriKind.Relative),
            new { body = "No token." },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("auth.antiforgery_failed");
    }

    [Fact]
    public async Task An_anonymous_caller_can_neither_read_nor_post()
    {
        var ticket = await TicketAsync();

        using var anonymous = fixture.CreateClient();

        (await anonymous.GetAsync(new Uri($"/api/v1/tickets/{ticket}/comments", UriKind.Relative), Token))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await TicketThreadClient.PostCommentAsync(anonymous, ticket, "Hello.", Token))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// SPEC.md §15 counts ticket modifications as mandatory coverage. There is no comment
    /// event — ARCHITECTURE.md §5 names none — so this row is written through
    /// <c>IAuditWriter</c> inside the request, which is why it needs no wait.
    /// </summary>
    /// <remarks>
    /// The body is deliberately absent from the diff: a new comment has no "before", and
    /// copying it into an append-only table that can never be corrected buys nothing the
    /// comment row does not already hold.
    /// </remarks>
    [Fact]
    public async Task Posting_writes_an_audit_row_against_the_ticket()
    {
        var ticket = await TicketAsync();
        var techId = await TicketClient.UserIdAsync(fixture, "tech", Token);

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var comment = await TicketThreadClient.CommentsAsync(
            technician, ticket, "Sensitive detail.", Token, isInternal: true);

        var rows = await AuditQueries.ByEntityAsync(fixture.DataSource, "Ticket", ticket.ToString(), Token);
        var row = rows.Single(entry => entry.Action == "helpdesk.ticket_commented");

        row.ActorId.ShouldBe(techId);
        row.Changes["commentId"].After.ShouldBe(comment.Id.ToString());
        row.Changes["isInternal"].After.ShouldBe("True");
        row.Changes.ShouldNotContainKey("body");
    }

    /// <summary>
    /// Written through the writer rather than derived from an event, so unlike a status
    /// change it happens inside the request and carries the caller's address.
    /// </summary>
    [Fact]
    public async Task The_audit_row_carries_the_source_address()
    {
        var ticket = await TicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        await TicketThreadClient.CommentsAsync(technician, ticket, "Noted.", Token);

        var rows = await AuditQueries.ByEntityAsync(fixture.DataSource, "Ticket", ticket.ToString(), Token);

        rows.Single(entry => entry.Action == "helpdesk.ticket_commented")
            .SourceIp.ShouldBe(IdentityWebFixture.RemoteIpAddress);
    }

    /// <summary>A refused comment must leave nothing behind — no row, no trail entry.</summary>
    [Fact]
    public async Task A_refused_comment_writes_neither_a_row_nor_an_audit_entry()
    {
        var ticket = await OwnTicketAsync();

        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        (await TicketThreadClient.PostCommentAsync(endUser, ticket, "Sneaky.", Token, isInternal: true))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        (await TicketThreadClient.ListCommentsAsync(technician, ticket, Token)).Total.ShouldBe(0);

        var rows = await AuditQueries.ByEntityAsync(fixture.DataSource, "Ticket", ticket.ToString(), Token);
        rows.ShouldNotContain(entry => entry.Action == "helpdesk.ticket_commented");
    }

    /// <summary>A ticket raised by and for the technician: not the end user's.</summary>
    private async Task<Guid> TicketAsync()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var ticket = await TicketClient.CreateAsync(
            technician, reference, departmentId, "Laptop will not charge", Token);

        return ticket.Id;
    }

    /// <summary>The same ticket, raised on the end user's behalf so it is theirs to read.</summary>
    private async Task<Guid> OwnTicketAsync()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var endUserId = await TicketClient.UserIdAsync(fixture, "user", Token);

        var ticket = await TicketClient.CreateAsync(
            technician, reference, departmentId, "Laptop will not charge", Token, requesterId: endUserId);

        return ticket.Id;
    }
}
