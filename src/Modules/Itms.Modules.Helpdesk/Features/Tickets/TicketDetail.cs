namespace Itms.Modules.Helpdesk.Features.Tickets;

/// <summary>
/// A ticket detail together with the row version it was read at.
/// </summary>
/// <remarks>
/// <para>
/// The version is not part of <see cref="TicketDetailResponse"/> because it is not part of
/// the ticket: it is a fact about <em>this read</em>, and HTTP already has a place for
/// that. It travels to the client as an <c>ETag</c> header and comes back as
/// <c>If-Match</c> — never in the body, where a client would be tempted to store it
/// alongside the ticket and send it back long after it stopped being true.
/// </para>
/// <para>
/// Internal, so the shape never reaches the OpenAPI document. What the contract describes
/// is the response and the header.
/// </para>
/// </remarks>
/// <param name="Response">The ticket as the client sees it.</param>
/// <param name="Version">The <c>xmin</c> row version at the moment of the read.</param>
internal sealed record TicketDetail(TicketDetailResponse Response, uint Version);
