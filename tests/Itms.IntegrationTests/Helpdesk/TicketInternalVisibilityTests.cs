using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// WP-1.7's two done-criteria, asserted at the API level and nowhere else.
/// </summary>
/// <remarks>
/// <para>
/// <b>"A User fetching their own ticket receives no internal notes in the API payload —
/// verified at the API level, not just the UI."</b> The distinction matters: a screen that
/// declines to render an internal note is still a screen that was handed one, and anybody
/// with a browser's network tab would read it. So these tests read the payload the server
/// actually sent, and several of them assert on the raw JSON text rather than on a
/// deserialised shape — a DTO that has no field for something cannot notice that the field
/// was there.
/// </para>
/// <para>
/// <b>"An attachment cannot be fetched by a user without access to its ticket."</b> Two
/// senses of "without access", both covered: somebody else's ticket entirely, and an
/// internal attachment on a ticket that is genuinely theirs.
/// </para>
/// <para>
/// The ticket in every test below is <em>the end user's own</em>. That is the whole point:
/// <c>TicketScope</c> already refuses tickets they did not raise, and it would have passed
/// every one of these while leaking every note.
/// </para>
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketInternalVisibilityTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private const string PublicComment = "We have ordered a replacement charger.";
    private const string InternalNote = "Third failure this quarter; the batch is suspect.";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// The criterion itself, against the bytes on the wire.
    /// </summary>
    /// <remarks>
    /// Asserting on the raw response text rather than on <c>TicketDetailDto</c> is
    /// deliberate. Deserialising into a shape that models what the requester is *supposed*
    /// to receive would quietly discard anything extra, so the test would keep passing on
    /// the day the projection started including notes.
    /// </remarks>
    [Fact]
    public async Task A_requester_reading_their_own_ticket_receives_no_internal_note_anywhere_in_the_payload()
    {
        var ticket = await OwnTicketWithBothKindsAsync();

        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var response = await endUser.GetAsync(new Uri($"/api/v1/tickets/{ticket}", UriKind.Relative), Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadAsStringAsync(Token);

        payload.ShouldContain(PublicComment);
        payload.ShouldNotContain(InternalNote);
    }

    /// <summary>
    /// The technician's view of the same ticket, so the test above is known to be asserting
    /// on a filter rather than on a note that was never written.
    /// </summary>
    [Fact]
    public async Task A_technician_reading_the_same_ticket_receives_both()
    {
        var ticket = await OwnTicketWithBothKindsAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var (detail, _) = await TicketClient.GetAsync(technician, ticket, Token);

        detail.Comments.Count.ShouldBe(2);
        detail.Comments.ShouldContain(comment => comment.Body == InternalNote && comment.IsInternal);
        detail.Comments.ShouldContain(comment => comment.Body == PublicComment && !comment.IsInternal);
    }

    [Fact]
    public async Task The_comment_list_endpoint_withholds_internal_notes_from_the_requester()
    {
        var ticket = await OwnTicketWithBothKindsAsync();

        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var page = await TicketThreadClient.ListCommentsAsync(endUser, ticket, Token);

        page.Items.Count.ShouldBe(1);
        page.Items[0].Body.ShouldBe(PublicComment);
        page.Items[0].IsInternal.ShouldBeFalse();
    }

    /// <summary>
    /// A total counted before the filter would announce in a number exactly what the list
    /// withheld — "1 of 2 shown" tells a requester their technician wrote something about
    /// them, which is the fact an internal note exists to keep.
    /// </summary>
    [Fact]
    public async Task The_total_counts_what_the_caller_can_see_and_not_what_was_withheld()
    {
        var ticket = await OwnTicketWithBothKindsAsync();

        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        (await TicketThreadClient.ListCommentsAsync(endUser, ticket, Token)).Total.ShouldBe(1);
        (await TicketThreadClient.ListCommentsAsync(technician, ticket, Token)).Total.ShouldBe(2);
    }

    /// <summary>
    /// The same reasoning as the total, one level subtler: <c>hasMoreComments</c> is decided
    /// from the filtered query, so a ticket whose hidden notes push it past the embedded
    /// count does not raise the flag for somebody who cannot see them.
    /// </summary>
    [Fact]
    public async Task The_has_more_flag_is_decided_from_what_the_caller_can_see()
    {
        var ticket = await OwnTicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        // Two public comments, and enough internal notes that the true total is well past
        // the embedded page.
        await TicketThreadClient.CommentsAsync(technician, ticket, PublicComment, Token);
        await TicketThreadClient.CommentsAsync(endUser, ticket, "Thank you.", Token);

        for (var i = 0; i < 30; i++)
        {
            await TicketThreadClient.CommentsAsync(technician, ticket, $"{InternalNote} {i}", Token, isInternal: true);
        }

        var (asUser, _) = await TicketClient.GetAsync(endUser, ticket, Token);
        asUser.Comments.Count.ShouldBe(2);
        asUser.HasMoreComments.ShouldBeFalse();

        var (asTechnician, _) = await TicketClient.GetAsync(technician, ticket, Token);
        asTechnician.Comments.Count.ShouldBe(25);
        asTechnician.HasMoreComments.ShouldBeTrue();
    }

    [Fact]
    public async Task A_requester_does_not_see_an_internal_attachment_on_their_own_ticket()
    {
        var ticket = await OwnTicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        await TicketThreadClient.AttachesAsync(technician, ticket, "public.png", Token);
        await TicketThreadClient.AttachesAsync(technician, ticket, "batch-analysis.png", Token, isInternal: true);

        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var response = await endUser.GetAsync(new Uri($"/api/v1/tickets/{ticket}", UriKind.Relative), Token);
        var payload = await response.Content.ReadAsStringAsync(Token);

        payload.ShouldContain("public.png");
        payload.ShouldNotContain("batch-analysis.png");

        var page = await TicketThreadClient.ListAttachmentsAsync(endUser, ticket, Token);
        page.Total.ShouldBe(1);
        page.Items.ShouldAllBe(attachment => !attachment.IsInternal);
    }

    /// <summary>
    /// The second criterion, in the sense that matters most: the requester holds a real
    /// attachment id on a ticket that really is theirs, and still cannot have the bytes.
    /// </summary>
    /// <remarks>
    /// They cannot normally learn the id — the list withholds it — so this hands it to them
    /// directly, which is exactly the guess the download has to survive.
    /// </remarks>
    [Fact]
    public async Task A_requester_cannot_download_an_internal_attachment_on_their_own_ticket()
    {
        var ticket = await OwnTicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        var internalFile = await TicketThreadClient.AttachesAsync(
            technician, ticket, "batch-analysis.png", Token, isInternal: true);

        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var response = await TicketThreadClient.DownloadAsync(endUser, ticket, internalFile.Id, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("helpdesk.attachment_not_found");
    }

    /// <summary>
    /// The other sense of the criterion: a ticket that is not theirs at all.
    /// </summary>
    [Fact]
    public async Task A_user_cannot_download_an_attachment_on_somebody_elses_ticket()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        // Raised by and for the technician: the end user has nothing to do with it.
        var other = await TicketClient.CreateAsync(technician, reference, departmentId, "Switch fault", Token);
        var file = await TicketThreadClient.AttachesAsync(technician, other.Id, "trace.png", Token);

        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var response = await TicketThreadClient.DownloadAsync(endUser, other.Id, file.Id, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The ticket id in the route is part of the check, not a label. An attachment reached
    /// through a ticket it does not belong to is not found even for somebody who could have
    /// had it through the right one.
    /// </summary>
    [Fact]
    public async Task An_attachment_fetched_through_the_wrong_ticket_is_not_found()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);

        var first = await TicketClient.CreateAsync(technician, reference, departmentId, "First", Token);
        var second = await TicketClient.CreateAsync(technician, reference, departmentId, "Second", Token);
        var file = await TicketThreadClient.AttachesAsync(technician, first.Id, "trace.png", Token);

        // The same technician, who may read both tickets and this attachment.
        (await TicketThreadClient.DownloadAsync(technician, first.Id, file.Id, Token))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await TicketThreadClient.DownloadAsync(technician, second.Id, file.Id, Token))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A requester may not write a note they could not then read. Refused outright rather
    /// than downgraded to a public comment: the author would otherwise believe they had
    /// written something private, while the requester read it.
    /// </summary>
    [Fact]
    public async Task A_requester_asking_for_an_internal_note_is_refused_rather_than_downgraded()
    {
        var ticket = await OwnTicketAsync();

        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var response = await TicketThreadClient.PostCommentAsync(
            endUser, ticket, "Trying to be sneaky.", Token, isInternal: true);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("helpdesk.internal_not_permitted");

        // And nothing was written: not as an internal note, and not as a public comment.
        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        (await TicketThreadClient.ListCommentsAsync(technician, ticket, Token)).Total.ShouldBe(0);
    }

    [Fact]
    public async Task A_requester_asking_for_an_internal_attachment_is_refused_rather_than_downgraded()
    {
        var ticket = await OwnTicketAsync();

        using var endUser = await AuthClient.SignedInAsync(fixture, "user", Token);

        var response = await TicketThreadClient.PostAttachmentAsync(
            endUser, ticket, "sneaky.png", TicketThreadClient.Png, Token, isInternal: true);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);
        (await TicketThreadClient.ListAttachmentsAsync(technician, ticket, Token)).Total.ShouldBe(0);
    }

    /// <summary>An end user's own ticket, raised by a technician on their behalf.</summary>
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

    /// <summary>That ticket, with one comment of each kind on it.</summary>
    private async Task<Guid> OwnTicketWithBothKindsAsync()
    {
        var ticket = await OwnTicketAsync();

        using var technician = await AuthClient.SignedInAsync(fixture, "tech", Token);

        await TicketThreadClient.CommentsAsync(technician, ticket, PublicComment, Token);
        await TicketThreadClient.CommentsAsync(technician, ticket, InternalNote, Token, isInternal: true);

        return ticket;
    }
}
