using System.Net.Http.Json;
using System.Text.Json;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.DirectoryModule;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>One line of a ticket's timeline, as the suite reads it off the wire.</summary>
/// <param name="Id">The entry's id.</param>
/// <param name="Kind">Which dimension moved.</param>
/// <param name="FromValue">What it read before.</param>
/// <param name="ToValue">What it reads now.</param>
/// <param name="OccurredAt">When the change happened.</param>
/// <param name="Sequence">Where the line sits among the lines one change wrote.</param>
/// <param name="ActorId">Who made it.</param>
/// <param name="ActorName">Their display name at the time.</param>
public sealed record TicketHistoryDto(
    Guid Id,
    TicketChangeKind Kind,
    string? FromValue,
    string? ToValue,
    DateTimeOffset OccurredAt,
    int Sequence,
    Guid? ActorId,
    string? ActorName);

/// <summary>A ticket's SLA clocks, as the API renders them.</summary>
/// <param name="ResponseTargetMinutes">Minutes allowed for a response.</param>
/// <param name="ResponseDueAt">When the response target expires.</param>
/// <param name="ResponseWarnAt">When 80% of the response target is consumed.</param>
/// <param name="RespondedAt">When somebody first answered, or null.</param>
/// <param name="ResolutionTargetMinutes">Minutes allowed for a resolution.</param>
/// <param name="ResolutionDueAt">When the resolution target expires, pauses included.</param>
/// <param name="ResolutionWarnAt">When 80% of the resolution target is consumed.</param>
/// <param name="ResolvedAt">When the resolution clock stopped, or null.</param>
/// <param name="PausedAt">When the ticket entered Waiting, or null.</param>
/// <param name="ResponseState">Where the response clock stands.</param>
/// <param name="ResolutionState">Where the resolution clock stands.</param>
/// <param name="IsPaused">Whether the resolution clock is parked.</param>
/// <param name="PausedSeconds">How long the ticket has spent Waiting in total.</param>
public sealed record TicketSlaDto(
    int ResponseTargetMinutes,
    DateTimeOffset ResponseDueAt,
    DateTimeOffset ResponseWarnAt,
    DateTimeOffset? RespondedAt,
    int ResolutionTargetMinutes,
    DateTimeOffset ResolutionDueAt,
    DateTimeOffset ResolutionWarnAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? PausedAt,
    SlaState ResponseState,
    SlaState ResolutionState,
    bool IsPaused,
    long PausedSeconds);

/// <summary>The asset a ticket names, as the detail and the link response render it.</summary>
/// <param name="Id">The asset's id.</param>
/// <param name="AssetTag">Its unique, immutable tag.</param>
/// <param name="Name">Its display name, falling back to the tag.</param>
/// <param name="AssetType">What kind of thing it is.</param>
/// <param name="Status">Its lifecycle status, as the stable code.</param>
public sealed record TicketRelatedAssetDto(
    Guid Id,
    string AssetTag,
    string Name,
    string AssetType,
    string Status);

/// <summary>A link change as the suite reads it off the wire.</summary>
/// <param name="Id">The ticket.</param>
/// <param name="Number">Its human-readable number.</param>
/// <param name="PreviousAsset">The asset it named before, or null.</param>
/// <param name="RelatedAsset">The asset it names now, or null.</param>
/// <param name="ChangedAt">When the link changed.</param>
public sealed record TicketAssetLinkDto(
    Guid Id,
    string Number,
    TicketRelatedAssetDto? PreviousAsset,
    TicketRelatedAssetDto? RelatedAsset,
    DateTimeOffset ChangedAt);

/// <summary>
/// A ticket as another module's panel renders it — the shape <c>ITicketLookup</c> hands
/// out, which is what the asset and user pages return.
/// </summary>
/// <param name="Id">The ticket's id.</param>
/// <param name="Number">Its human-readable number.</param>
/// <param name="Subject">The one-line summary.</param>
/// <param name="Status">Where it sits in the workflow, as a string.</param>
/// <param name="PriorityCode">Its priority's stable key.</param>
/// <param name="PriorityRank">That priority's ordering weight.</param>
/// <param name="RequesterId">Who it is for.</param>
/// <param name="AssigneeId">The technician holding it, or null.</param>
/// <param name="RelatedAssetId">The asset it concerns, or null.</param>
/// <param name="IsOpen">False once resolved, closed, or cancelled.</param>
/// <param name="CreatedAt">When it was raised.</param>
/// <param name="DueAt">When resolution is due.</param>
/// <param name="ResolvedAt">When it was resolved, or null.</param>
/// <param name="ClosedAt">When it was closed, or null.</param>
public sealed record TicketSummaryDto(
    Guid Id,
    string Number,
    string Subject,
    string Status,
    string PriorityCode,
    int PriorityRank,
    Guid RequesterId,
    Guid? AssigneeId,
    Guid? RelatedAssetId,
    bool IsOpen,
    DateTimeOffset CreatedAt,
    DateTimeOffset DueAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt);

/// <summary>A ticket as the detail endpoint renders it.</summary>
/// <param name="Id">The ticket's id.</param>
/// <param name="Number">The human-readable number.</param>
/// <param name="Subject">The one-line summary.</param>
/// <param name="Description">What was reported.</param>
/// <param name="Status">Where it sits in the workflow.</param>
/// <param name="CategoryId">Its category.</param>
/// <param name="CategoryName">That category's name.</param>
/// <param name="PriorityId">Its priority.</param>
/// <param name="PriorityName">That priority's name.</param>
/// <param name="PriorityCode">That priority's stable key.</param>
/// <param name="PriorityRank">Its ordering weight.</param>
/// <param name="RequesterId">Who it is for.</param>
/// <param name="RequesterName">Their cached display name.</param>
/// <param name="DepartmentId">The department it is filed against.</param>
/// <param name="DepartmentName">That department's cached name.</param>
/// <param name="AssigneeId">The technician holding it, or null.</param>
/// <param name="AssigneeName">Their cached display name, or null.</param>
/// <param name="ResolutionNotes">What was done, once resolved.</param>
/// <param name="ResolvedAt">When it was resolved.</param>
/// <param name="ClosedAt">When it was closed.</param>
/// <param name="RelatedAssetId">The asset it concerns. WP-2.5.</param>
/// <param name="RelatedAlertId">The alert it came from. WP-3.7.</param>
/// <param name="CreatedAt">When it was raised.</param>
/// <param name="UpdatedAt">When it last moved.</param>
/// <param name="DueAt">When resolution is due, pauses included.</param>
/// <param name="Sla">Both SLA clocks and where each stands.</param>
/// <param name="AllowedNextStatuses">The moves it may still make.</param>
/// <param name="History">The head of its timeline, newest first.</param>
/// <param name="HasMoreHistory">True when the timeline is longer than what is embedded.</param>
/// <param name="Comments">The head of its conversation, filtered to what the caller may read.</param>
/// <param name="HasMoreComments">True when there are more comments the caller may read.</param>
/// <param name="Attachments">Its attachments, filtered to what the caller may see.</param>
/// <param name="HasMoreAttachments">True when there are more attachments the caller may see.</param>
/// <param name="RelatedAsset">The asset it names, read live at the moment of the request.</param>
public sealed record TicketDetailDto(
    Guid Id,
    string Number,
    string Subject,
    string Description,
    TicketStatus Status,
    Guid CategoryId,
    string CategoryName,
    Guid PriorityId,
    string PriorityName,
    string PriorityCode,
    int PriorityRank,
    Guid RequesterId,
    string RequesterName,
    Guid DepartmentId,
    string DepartmentName,
    Guid? AssigneeId,
    string? AssigneeName,
    string? ResolutionNotes,
    string? HoldReason,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    Guid? RelatedAssetId,
    Guid? RelatedAlertId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset DueAt,
    TicketSlaDto Sla,
    IReadOnlyList<TicketStatus> AllowedNextStatuses,
    IReadOnlyList<TicketHistoryDto> History,
    bool HasMoreHistory,
    IReadOnlyList<TicketCommentDto> Comments,
    bool HasMoreComments,
    IReadOnlyList<TicketAttachmentDto> Attachments,
    bool HasMoreAttachments,
    TicketRelatedAssetDto? RelatedAsset);

/// <summary>One row of the queue, as the list endpoint renders it.</summary>
/// <param name="Id">The ticket's id.</param>
/// <param name="Number">The human-readable number.</param>
/// <param name="Subject">The one-line summary.</param>
/// <param name="Status">Where it sits in the workflow.</param>
/// <param name="CategoryId">Its category.</param>
/// <param name="CategoryName">That category's name.</param>
/// <param name="PriorityId">Its priority.</param>
/// <param name="PriorityName">That priority's name.</param>
/// <param name="PriorityCode">That priority's stable key.</param>
/// <param name="PriorityRank">Its ordering weight.</param>
/// <param name="RequesterId">Who it is for.</param>
/// <param name="RequesterName">Their cached display name.</param>
/// <param name="DepartmentId">The department it is filed against.</param>
/// <param name="DepartmentName">That department's cached name.</param>
/// <param name="AssigneeId">The technician holding it, or null.</param>
/// <param name="AssigneeName">Their cached display name, or null.</param>
/// <param name="CreatedAt">When it was raised.</param>
/// <param name="UpdatedAt">When it last moved.</param>
/// <param name="DueAt">When resolution is due, pauses included.</param>
/// <param name="Sla">Both SLA clocks and where each stands.</param>
public sealed record TicketListItemDto(
    Guid Id,
    string Number,
    string Subject,
    TicketStatus Status,
    Guid CategoryId,
    string CategoryName,
    Guid PriorityId,
    string PriorityName,
    string PriorityCode,
    int PriorityRank,
    Guid RequesterId,
    string RequesterName,
    Guid DepartmentId,
    string DepartmentName,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset DueAt,
    TicketSlaDto Sla);

/// <summary>
/// The ticket request shapes every WP-1.5 suite needs, written once.
/// </summary>
/// <remarks>
/// Unlike <c>TicketWriter</c>, everything here goes over the wire through the real
/// endpoints. <c>TicketWriter</c> stays for the two things HTTP cannot reach — arranging a
/// starting status past <c>Assigned</c> without walking transitions that are themselves
/// under test, and failing a transaction from inside.
/// </remarks>
internal static class TicketClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The route the ticket endpoints hang off.</summary>
    public const string Tickets = "/api/v1/tickets";

    /// <summary>
    /// A department to file tickets against.
    /// </summary>
    /// <remarks>
    /// Created rather than assumed: WP-0.6 deliberately does not re-seed the development
    /// directory in <c>ResetAsync</c>, so every suite starts from an empty tree and builds
    /// exactly what it asserts on. The seeded development accounts carry no department
    /// either — <c>users.department_id</c> exists and nothing populates it yet — so a
    /// create request has to name one.
    /// </remarks>
    /// <param name="admin">An admin client, since department writes are Admin-only.</param>
    /// <param name="name">The department name.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The created department's id.</returns>
    public static async Task<Guid> DepartmentAsync(
        HttpClient admin,
        string name,
        CancellationToken cancellationToken)
    {
        var department = await DirectoryClient.CreateDepartmentAsync(admin, name, code: null, cancellationToken);
        return department.Id;
    }

    /// <summary>Raises a ticket and returns it, failing loudly if the call did not succeed.</summary>
    /// <param name="client">The signed-in client raising it.</param>
    /// <param name="reference">The category and priority to file against.</param>
    /// <param name="departmentId">The department to file against.</param>
    /// <param name="subject">The subject line.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="requesterId">Who it is for, or null for the caller.</param>
    /// <param name="priorityId">Overrides the priority in <paramref name="reference"/>.</param>
    /// <returns>The created ticket.</returns>
    public static async Task<TicketDetailDto> CreateAsync(
        HttpClient client,
        TicketWriter.ReferenceData reference,
        Guid departmentId,
        string subject,
        CancellationToken cancellationToken,
        Guid? requesterId = null,
        Guid? priorityId = null)
    {
        var response = await PostAsync(
            client,
            reference,
            departmentId,
            subject,
            cancellationToken,
            requesterId,
            priorityId);

        response.EnsureSuccessStatusCode();
        return await ApiClient.ReadAsync<TicketDetailDto>(response, cancellationToken);
    }

    /// <summary>Posts a create request and hands back the raw response, so a test can assert on a refusal.</summary>
    /// <param name="client">The signed-in client raising it.</param>
    /// <param name="reference">The category and priority to file against.</param>
    /// <param name="departmentId">The department to file against, or null to omit it.</param>
    /// <param name="subject">The subject line.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="requesterId">Who it is for, or null to omit it.</param>
    /// <param name="priorityId">Overrides the priority in <paramref name="reference"/>.</param>
    /// <param name="categoryId">Overrides the category in <paramref name="reference"/>.</param>
    /// <returns>The raw response.</returns>
    public static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        TicketWriter.ReferenceData reference,
        Guid? departmentId,
        string subject,
        CancellationToken cancellationToken,
        Guid? requesterId = null,
        Guid? priorityId = null,
        Guid? categoryId = null) =>
        ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            Tickets,
            new
            {
                subject,
                description = "It stops charging at 40% and the light goes amber.",
                categoryId = categoryId ?? reference.CategoryId,
                priorityId = priorityId ?? reference.PriorityId,
                requesterId,
                departmentId,
            },
            cancellationToken);

    /// <summary>Reads one ticket, returning both the body and the ETag the response carried.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="ticketId">The ticket to read.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The ticket and its entity tag.</returns>
    public static async Task<(TicketDetailDto Ticket, string ETag)> GetAsync(
        HttpClient client,
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var response = await client.GetAsync(new Uri($"{Tickets}/{ticketId}", UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();

        var ticket = await ApiClient.ReadAsync<TicketDetailDto>(response, cancellationToken);
        return (ticket, response.Headers.ETag?.ToString() ?? string.Empty);
    }

    /// <summary>Moves a ticket's status, optionally stating an <c>If-Match</c> precondition.</summary>
    /// <param name="client">A technician or admin client.</param>
    /// <param name="ticketId">The ticket to move.</param>
    /// <param name="status">The destination.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="resolutionNotes">The resolution, when resolving.</param>
    /// <param name="ifMatch">The entity tag to require, or null to state no precondition.</param>
    /// <param name="holdReason">What the ticket is waiting on, when holding.</param>
    /// <returns>The raw response.</returns>
    public static async Task<HttpResponseMessage> ChangeStatusAsync(
        HttpClient client,
        Guid ticketId,
        TicketStatus status,
        CancellationToken cancellationToken,
        string? resolutionNotes = null,
        string? ifMatch = null,
        string? holdReason = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{Tickets}/{ticketId}/status-changes")
        {
            Content = JsonContent.Create(
                new { status = status.ToString(), resolutionNotes, holdReason },
                options: Json),
        };

        if (ifMatch is not null)
        {
            // Added without validation, so a test can send a deliberately malformed tag.
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        var csrf = await client.GetFromJsonAsync<CsrfDto>(
            new Uri("/api/v1/auth/csrf", UriKind.Relative),
            Json,
            cancellationToken)
            ?? throw new InvalidOperationException("No antiforgery token was issued.");

        request.Headers.Add(csrf.HeaderName, csrf.Token);
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>Changes a ticket's assignee, optionally stating an <c>If-Match</c> precondition.</summary>
    /// <remarks>
    /// Built by hand rather than through <c>ApiClient.SendAsync</c> for the reason
    /// <see cref="ChangeStatusAsync"/> is: a test has to be able to attach a deliberately
    /// malformed <c>If-Match</c> without <c>HttpClient</c> validating it away.
    /// </remarks>
    /// <param name="client">A technician or admin client.</param>
    /// <param name="ticketId">The ticket to assign.</param>
    /// <param name="assigneeId">Who takes it on, or null to unassign.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="ifMatch">The entity tag to require, or null to state no precondition.</param>
    /// <returns>The raw response.</returns>
    public static async Task<HttpResponseMessage> AssignAsync(
        HttpClient client,
        Guid ticketId,
        Guid? assigneeId,
        CancellationToken cancellationToken,
        string? ifMatch = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{Tickets}/{ticketId}/assignments")
        {
            Content = JsonContent.Create(new { assigneeId }, options: Json),
        };

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        var csrf = await client.GetFromJsonAsync<CsrfDto>(
            new Uri("/api/v1/auth/csrf", UriKind.Relative),
            Json,
            cancellationToken)
            ?? throw new InvalidOperationException("No antiforgery token was issued.");

        request.Headers.Add(csrf.HeaderName, csrf.Token);
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>Assigns a ticket and returns the result, failing loudly if the call did not succeed.</summary>
    /// <param name="client">A technician or admin client.</param>
    /// <param name="ticketId">The ticket to assign.</param>
    /// <param name="assigneeId">Who takes it on, or null to unassign.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The assignment, and the ETag the response carried.</returns>
    public static async Task<(TicketAssignmentDto Assignment, string ETag)> AssignsAsync(
        HttpClient client,
        Guid ticketId,
        Guid? assigneeId,
        CancellationToken cancellationToken)
    {
        var response = await AssignAsync(client, ticketId, assigneeId, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (
            await ApiClient.ReadAsync<TicketAssignmentDto>(response, cancellationToken),
            response.Headers.ETag?.ToString() ?? string.Empty);
    }

    /// <summary>Changes the asset a ticket names, optionally stating an <c>If-Match</c> precondition.</summary>
    /// <remarks>
    /// Built by hand rather than through <c>ApiClient.SendAsync</c> for the reason
    /// <see cref="ChangeStatusAsync"/> is: a test has to be able to attach a deliberately
    /// stale <c>If-Match</c> without <c>HttpClient</c> validating it away.
    /// </remarks>
    /// <param name="client">A technician or admin client.</param>
    /// <param name="ticketId">The ticket to link.</param>
    /// <param name="assetId">The asset it concerns, or null to clear the link.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="ifMatch">The entity tag to require, or null to state no precondition.</param>
    /// <returns>The raw response.</returns>
    public static async Task<HttpResponseMessage> LinkAssetAsync(
        HttpClient client,
        Guid ticketId,
        Guid? assetId,
        CancellationToken cancellationToken,
        string? ifMatch = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{Tickets}/{ticketId}/related-asset")
        {
            Content = JsonContent.Create(new { assetId }, options: Json),
        };

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        var csrf = await client.GetFromJsonAsync<CsrfDto>(
            new Uri("/api/v1/auth/csrf", UriKind.Relative),
            Json,
            cancellationToken)
            ?? throw new InvalidOperationException("No antiforgery token was issued.");

        request.Headers.Add(csrf.HeaderName, csrf.Token);
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>Links an asset and returns the result, failing loudly if the call did not succeed.</summary>
    /// <param name="client">A technician or admin client.</param>
    /// <param name="ticketId">The ticket to link.</param>
    /// <param name="assetId">The asset it concerns, or null to clear the link.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The link, and the ETag the response carried.</returns>
    public static async Task<(TicketAssetLinkDto Link, string ETag)> LinksAssetAsync(
        HttpClient client,
        Guid ticketId,
        Guid? assetId,
        CancellationToken cancellationToken)
    {
        var response = await LinkAssetAsync(client, ticketId, assetId, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (
            await ApiClient.ReadAsync<TicketAssetLinkDto>(response, cancellationToken),
            response.Headers.ETag?.ToString() ?? string.Empty);
    }

    /// <summary>Reads a page of the queue.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="query">The query string, without the leading question mark.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The page envelope.</returns>
    public static Task<Api.PageDto<TicketListItemDto>> ListAsync(
        HttpClient client,
        string query,
        CancellationToken cancellationToken) =>
        ApiClient.ListAsync<TicketListItemDto>(
            client,
            query.Length == 0 ? Tickets : $"{Tickets}?{query}",
            cancellationToken);

    /// <summary>Reads the queue's counters.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <param name="dueBefore">The caller's own end of day, or null to let the server pick.</param>
    /// <returns>The counters.</returns>
    public static async Task<TicketCountersDto> CountersAsync(
        HttpClient client,
        CancellationToken cancellationToken,
        DateTimeOffset? dueBefore = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        var path = dueBefore is { } instant
            ? $"{Tickets}/counters?dueBefore={Uri.EscapeDataString(instant.ToString("O", System.Globalization.CultureInfo.InvariantCulture))}"
            : $"{Tickets}/counters";

        var response = await client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();

        return await ApiClient.ReadAsync<TicketCountersDto>(response, cancellationToken);
    }

    /// <summary>The id of one of the seeded development accounts.</summary>
    /// <param name="fixture">The booted host.</param>
    /// <param name="userName">The account: <c>admin</c>, <c>tech</c>, or <c>user</c>.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>That account's user id.</returns>
    public static async Task<Guid> UserIdAsync(
        IdentityWebFixture fixture,
        string userName,
        CancellationToken cancellationToken)
    {
        using var client = await AuthClient.SignedInAsync(fixture, userName, cancellationToken);
        var response = await AuthClient.MeAsync(client, cancellationToken);
        response.EnsureSuccessStatusCode();

        var me = await AuthClient.ReadUserAsync(response, cancellationToken);
        return me.Id;
    }

    private sealed record CsrfDto(string HeaderName, string Token);
}

/// <summary>The queue's counters as the suite reads them off the wire.</summary>
public sealed record TicketCountersDto(
    int All,
    int Open,
    int Unassigned,
    int Overdue,
    int DueToday,
    int Mine);

/// <summary>An assignment as the suite reads it off the wire.</summary>
/// <param name="Id">The ticket.</param>
/// <param name="Number">Its human-readable number.</param>
/// <param name="PreviousAssigneeId">Who held it before, or null.</param>
/// <param name="PreviousAssigneeName">Their cached display name, or null.</param>
/// <param name="AssigneeId">Who holds it now, or null.</param>
/// <param name="AssigneeName">Their cached display name, or null.</param>
/// <param name="PreviousStatus">The status before the assignment.</param>
/// <param name="Status">The status after it.</param>
/// <param name="ChangedAt">When it happened.</param>
/// <param name="AllowedNextStatuses">The moves the ticket may still make.</param>
public sealed record TicketAssignmentDto(
    Guid Id,
    string Number,
    Guid? PreviousAssigneeId,
    string? PreviousAssigneeName,
    Guid? AssigneeId,
    string? AssigneeName,
    TicketStatus PreviousStatus,
    TicketStatus Status,
    DateTimeOffset ChangedAt,
    IReadOnlyList<TicketStatus> AllowedNextStatuses);
