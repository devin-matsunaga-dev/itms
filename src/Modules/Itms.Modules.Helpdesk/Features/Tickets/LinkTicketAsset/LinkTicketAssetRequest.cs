namespace Itms.Modules.Helpdesk.Features.Tickets.LinkTicketAsset;

/// <summary>The body of <c>POST /api/v1/tickets/{id}/related-asset</c>.</summary>
/// <remarks>
/// <para>
/// One route and one body cover linking, relinking, and unlinking, for the reason
/// <c>AssignTicketRequest</c> gives: all three are the same fact — which asset this ticket
/// is about — and all three write one history line.
/// </para>
/// <para>
/// Posted as a resource rather than patched onto the ticket, following the assignment and
/// the status change: the link is an event in the ticket's life that WP-1.4 records a row
/// per, and a refused one has to be distinguishable from a field that was not sent.
/// </para>
/// </remarks>
/// <param name="AssetId">
/// The asset the ticket concerns, or <see langword="null"/> to clear the link. A null is a
/// deliberate instruction, not an omitted field.
/// </param>
public sealed record LinkTicketAssetRequest(Guid? AssetId);
