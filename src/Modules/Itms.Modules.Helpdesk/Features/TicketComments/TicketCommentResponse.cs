using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.TicketComments;

/// <summary>One line of a ticket's conversation, as the API renders it.</summary>
/// <remarks>
/// <para>
/// <b><see cref="IsInternal"/> is on the wire, and it is not how the rule is enforced.</b>
/// A payload a requester receives never contains an internal line at all — the query
/// filtered it out — so on their screen this field is always <see langword="false"/>. It is
/// here for the technician's screen, which shows both kinds and has to draw them
/// differently: WP-1.10's criterion is a "clear visual distinction", and a client cannot
/// make one from a flag it was not given.
/// </para>
/// <para>
/// The author's name is the text cached on the row, not a live lookup — a thread has to
/// stay readable after the account is renamed or deactivated, the same call every other
/// cached name on a ticket makes.
/// </para>
/// </remarks>
/// <param name="Id">The comment's id.</param>
/// <param name="TicketId">The ticket it belongs to.</param>
/// <param name="Body">What was said.</param>
/// <param name="IsInternal">True when this line is invisible to the requester.</param>
/// <param name="AuthorId">Who wrote it.</param>
/// <param name="AuthorName">Their display name at the time.</param>
/// <param name="CreatedAt">When it was posted (UTC).</param>
public sealed record TicketCommentResponse(
    Guid Id,
    Guid TicketId,
    string Body,
    bool IsInternal,
    Guid AuthorId,
    string AuthorName,
    DateTimeOffset CreatedAt)
{
    /// <summary>The projection every comment query uses, so one shape is built in one place.</summary>
    internal static System.Linq.Expressions.Expression<Func<TicketComment, TicketCommentResponse>> Projection() =>
        comment => new TicketCommentResponse(
            comment.Id,
            comment.TicketId,
            comment.Body,
            comment.IsInternal,
            comment.AuthorId,
            comment.AuthorName,
            comment.CreatedAt);
}
