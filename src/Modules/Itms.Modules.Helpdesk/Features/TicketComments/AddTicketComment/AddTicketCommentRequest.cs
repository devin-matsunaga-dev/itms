namespace Itms.Modules.Helpdesk.Features.TicketComments.AddTicketComment;

/// <summary>The body of <c>POST /api/v1/tickets/{id}/comments</c>.</summary>
/// <remarks>
/// <para>
/// One route posts both kinds, distinguished by <paramref name="IsInternal"/>, for the
/// reason the two share one table: they differ in audience and in nothing else, and a
/// second route would be a second handler that could forget the audience check.
/// </para>
/// <para>
/// <b>The flag defaults to <see langword="false"/>, and that default is the safe one.</b> A
/// client that omits it, or one written before internal notes existed, posts something the
/// requester can see — which is at worst redundant. The opposite default would hide a
/// comment from the person it was written for, every time somebody forgot a field.
/// </para>
/// </remarks>
/// <param name="Body">What is being said.</param>
/// <param name="IsInternal">
/// True to write a note only the queue can read. A User sending this is refused with 403
/// rather than quietly downgraded — see <c>HelpdeskErrors.InternalCommentForbidden</c>.
/// </param>
public sealed record AddTicketCommentRequest(string Body, bool IsInternal = false);
