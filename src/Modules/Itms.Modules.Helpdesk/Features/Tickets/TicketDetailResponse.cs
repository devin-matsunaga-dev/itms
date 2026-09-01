using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.TicketAttachments;
using Itms.Modules.Helpdesk.Features.TicketComments;
using Itms.Modules.Helpdesk.Features.TicketHistory;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Modules.Helpdesk.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.Tickets;

/// <summary>One ticket in full, as the detail screen reads it.</summary>
/// <remarks>
/// <para>
/// The queue row's whole shape, plus what only the detail needs: the description, the
/// resolution, the links, the moves the ticket can still make, and the head of its
/// timeline. WP-1.10 draws this.
/// </para>
/// <para>
/// <b><see cref="AllowedNextStatuses"/> comes from the server, and a client must not
/// restate the table.</b> WP-1.3 settled this for the transition response and the reason
/// applies twice over here: WP-1.10's criterion is that illegal transitions are not
/// rendered, and a screen that has just loaded a ticket needs to know which buttons to
/// draw as much as one that has just moved it. It is
/// <c>TicketStateMachine.DestinationsFrom</c> either way, so the two can never disagree.
/// </para>
/// <para>
/// <b>The conversation and the files arrived at WP-1.7</b>, embedded the way the timeline
/// is and filtered by <see cref="TicketVisibility"/> before they get here: a payload a
/// requester receives contains no internal note and no internal attachment, and no count
/// or flag that would tell them one exists.
/// </para>
/// <para>
/// <b>The related asset came at WP-2.5, and its display text does not travel the way the
/// requester's and the department's do.</b> Those are cached columns on the ticket row,
/// because the queue renders fifty thousand of them and cannot resolve a name per row; this
/// one is <see cref="RelatedAsset"/>, filled in by the handler from <c>IAssetLookup</c> at
/// the moment of the read. A single-row detail can afford the lookup, and what it buys is a
/// tag that is never stale.
/// </para>
/// <para>
/// <b>What is not here yet.</b> <see cref="RelatedAlertId"/> is still a bare id, because
/// nothing sets it until WP-3.7. When it lands it will want its display text alongside, and
/// <see cref="RelatedAsset"/> is the shape to follow.
/// </para>
/// </remarks>
/// <param name="Id">The ticket's id.</param>
/// <param name="Number">The human-readable number, <c>TKT-####</c>.</param>
/// <param name="Subject">The one-line summary.</param>
/// <param name="Description">What the requester reported.</param>
/// <param name="Status">Where it sits in the workflow.</param>
/// <param name="CategoryId">What it is about.</param>
/// <param name="CategoryName">That category's name, as it reads now.</param>
/// <param name="PriorityId">How urgent it is.</param>
/// <param name="PriorityName">That priority's name, as it reads now.</param>
/// <param name="PriorityCode">That priority's stable key, for colour and for rules.</param>
/// <param name="PriorityRank">Its ordering weight.</param>
/// <param name="RequesterId">Who the ticket is for.</param>
/// <param name="RequesterName">Their display name, cached at creation.</param>
/// <param name="DepartmentId">The department it is filed against.</param>
/// <param name="DepartmentName">That department's name, cached at creation.</param>
/// <param name="AssigneeId">The technician responsible, or <see langword="null"/>.</param>
/// <param name="AssigneeName">Their display name, or <see langword="null"/>.</param>
/// <param name="ResolutionNotes">What was done, once it has been resolved. Kept through a reopen.</param>
/// <param name="HoldReason">
/// What the ticket is waiting on while it is parked, and <see langword="null"/> whenever it
/// is not. Cleared on resuming, so it always describes the state the ticket is actually in;
/// every reason ever given stays in the ticket's history.
/// </param>
/// <param name="ResolvedAt">When it was resolved (UTC), or <see langword="null"/>.</param>
/// <param name="ClosedAt">When it was closed (UTC), or <see langword="null"/>.</param>
/// <param name="RelatedAssetId">
/// The asset it concerns, or <see langword="null"/>. <see cref="RelatedAsset"/> carries the
/// same asset's display text on a read; this is the bare id every write answers with.
/// </param>
/// <param name="RelatedAlertId">The alert it was raised from, or <see langword="null"/>. WP-3.7 sets it.</param>
/// <param name="CreatedAt">When it was raised (UTC).</param>
/// <param name="UpdatedAt">When it last moved (UTC).</param>
/// <param name="DueAt">
/// When resolution is due, pauses included. The same instant as <c>sla.resolutionDueAt</c>,
/// kept at the top level because the queue sorts on it and the wire has carried it since
/// WP-1.5.
/// </param>
/// <param name="Sla">Both SLA clocks, and where each stands right now.</param>
public sealed record TicketDetailResponse(
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
    TicketSlaResponse Sla)
{
    /// <summary>
    /// How many comments and attachments the detail carries without being asked.
    /// </summary>
    /// <remarks>
    /// The same number as the timeline and for the same reason: the normal detail view is
    /// one round trip, and anything longer is paged through the list endpoint. It is counted
    /// <em>after</em> the visibility filter, so a requester's page is filled with lines they
    /// can actually read rather than being padded out by notes that were then removed.
    /// </remarks>
    public const int EmbeddedThreadCount = 25;

    /// <summary>How many timeline entries the detail carries without being asked.</summary>
    /// <remarks>
    /// One page of history, at the human's direction, so the normal detail view is a
    /// single round trip. A ticket with a longer timeline than this is read on through
    /// <c>GET /api/v1/tickets/{id}/history</c>, which is paged and carries the total.
    /// </remarks>
    public const int EmbeddedHistoryCount = 25;

    /// <summary>
    /// The statuses this ticket may legally move to next, straight from the state
    /// machine. Empty once the ticket is terminal.
    /// </summary>
    /// <remarks>Set after projection: it is computed from the status, not stored.</remarks>
    public IReadOnlyCollection<TicketStatus> AllowedNextStatuses { get; init; } = [];

    /// <summary>
    /// The head of the ticket's timeline, newest first — at most
    /// <see cref="EmbeddedHistoryCount"/> entries.
    /// </summary>
    /// <remarks>
    /// Entries sharing an <c>occurredAt</c> came from one change and are meant to be
    /// rendered as one event, not as separate rows with the same timestamp. WP-1.4's
    /// <c>sequence</c> is what orders them.
    /// </remarks>
    public IReadOnlyList<TicketHistoryEntryResponse> History { get; init; } = [];

    /// <summary>
    /// True when the ticket's timeline is longer than what
    /// <see cref="History"/> carries, so a client knows to offer the paged endpoint.
    /// </summary>
    public bool HasMoreHistory { get; init; }

    /// <summary>
    /// The head of the ticket's conversation, newest first — at most
    /// <see cref="EmbeddedThreadCount"/> comments the caller is allowed to read.
    /// </summary>
    /// <remarks>
    /// A requester never sees an internal note here. That is enforced by the query, not by
    /// this shape: see <see cref="TicketVisibility"/>.
    /// </remarks>
    public IReadOnlyList<TicketCommentResponse> Comments { get; init; } = [];

    /// <summary>
    /// True when there are more comments the caller may read than
    /// <see cref="Comments"/> carries.
    /// </summary>
    public bool HasMoreComments { get; init; }

    /// <summary>
    /// The ticket's attachments, newest first — at most <see cref="EmbeddedThreadCount"/>
    /// of the ones the caller is allowed to see. Metadata only; the bytes come from the
    /// download route.
    /// </summary>
    public IReadOnlyList<TicketAttachmentResponse> Attachments { get; init; } = [];

    /// <summary>
    /// True when there are more attachments the caller may see than
    /// <see cref="Attachments"/> carries.
    /// </summary>
    public bool HasMoreAttachments { get; init; }

    /// <summary>
    /// The asset <see cref="RelatedAssetId"/> names, as it reads right now — or
    /// <see langword="null"/> when the ticket names none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set after projection, like <see cref="AllowedNextStatuses"/>, because it is not a
    /// column: it is another module's row, read through <c>IAssetLookup</c> on the way out.
    /// </para>
    /// <para>
    /// It is also null when the asset has been soft-deleted since it was linked, while
    /// <see cref="RelatedAssetId"/> still names it. That pairing is deliberate — the ticket
    /// records what it was linked to, and the detail shows what can still be shown.
    /// </para>
    /// </remarks>
    public TicketRelatedAssetResponse? RelatedAsset { get; init; }

    /// <summary>
    /// The same ticket with its SLA states filled in for <paramref name="now"/>.
    /// </summary>
    /// <remarks>
    /// Every route that returns a detail — the read, and the create that reads itself back
    /// through the same projection — calls this, because <see cref="Project"/> can only
    /// bring back the stored instants: the states are a comparison against <c>IClock</c>
    /// and the database is not the thing holding the clock.
    /// </remarks>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <returns>The detail, with <see cref="Sla"/> assessed.</returns>
    public TicketDetailResponse Assessed(DateTimeOffset now) =>
        this with { Sla = Sla.Assessed(Status, now) };

    /// <summary>
    /// Projects tickets to detail rows with their row versions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A join rather than a correlated subquery per name, and an inner join because both
    /// foreign keys are <c>NOT NULL</c> with <c>ON DELETE RESTRICT</c> — a ticket whose
    /// category or priority is missing cannot exist. The collection properties are not
    /// projected: they are filled in by the handler, because neither is a column.
    /// </para>
    /// <para>
    /// The <c>xmin</c> version rides along rather than being read in a second query,
    /// because the ETag has to describe the row this projection just read. Two reads could
    /// straddle a write and hand the client a tag for a ticket it was not given.
    /// </para>
    /// </remarks>
    /// <param name="tickets">The ticket query, already scoped.</param>
    /// <param name="database">The context, for the reference tables.</param>
    /// <returns>The projected query. Nothing has been executed.</returns>
    internal static IQueryable<TicketDetail> Project(
        IQueryable<Ticket> tickets,
        HelpdeskDbContext database) =>
        from ticket in tickets
        join category in database.TicketCategories on ticket.CategoryId equals category.Id
        join priority in database.TicketPriorities on ticket.PriorityId equals priority.Id
        select new TicketDetail(
            new TicketDetailResponse(
                ticket.Id,
                ticket.Number,
                ticket.Subject,
                ticket.Description,
                ticket.Status,
                category.Id,
                category.Name,
                priority.Id,
                priority.Name,
                priority.Code,
                priority.Rank,
                ticket.RequesterId,
                ticket.RequesterName,
                ticket.DepartmentId,
                ticket.DepartmentName,
                ticket.AssigneeId,
                ticket.AssigneeName,
                ticket.ResolutionNotes,
                ticket.HoldReason,
                ticket.ResolvedAt,
                ticket.ClosedAt,
                ticket.RelatedAssetId,
                ticket.RelatedAlertId,
                ticket.CreatedAt,
                ticket.UpdatedAt,
                ticket.DueAt,
                new TicketSlaResponse(
                    ticket.ResponseTargetMinutes,
                    ticket.ResponseDueAt,
                    ticket.ResponseWarnAt,
                    ticket.RespondedAt,
                    ticket.ResolutionTargetMinutes,
                    ticket.DueAt,
                    ticket.ResolutionWarnAt,
                    ticket.ResolvedAt,
                    ticket.SlaPausedAt,
                    ticket.SlaPausedTotal)),
            EF.Property<uint>(ticket, TicketConfiguration.VersionProperty));
}
