using Itms.Contracts.Lookups;

namespace Itms.Modules.Helpdesk.Features.Tickets;

/// <summary>An asset as a ticket names it, resolved through <c>IAssetLookup</c>.</summary>
/// <remarks>
/// <para>
/// Helpdesk's own wire shape rather than <c>Itms.Contracts.Lookups.AssetSummary</c>, which
/// is what the module reads in process. The contract's shape carries the holder and the
/// location, which are facts about the asset that a ticket screen has no business
/// rendering and no reason to receive; this is the four fields a link is drawn from.
/// </para>
/// <para>
/// <b>It is never stored.</b> Every field here is read at the moment the ticket is read,
/// so a renamed or relabelled asset is right on the next request — the decision WP-2.5
/// took, at the human's direction, in preference to a cached tag column that nothing
/// would refresh.
/// </para>
/// </remarks>
/// <param name="Id">The asset's id.</param>
/// <param name="AssetTag">The identifier on the physical label. Unique and immutable.</param>
/// <param name="Name">Its display name, falling back to the tag when it has none.</param>
/// <param name="AssetType">What kind of thing it is.</param>
/// <param name="Status">Its lifecycle status, as the stable code a client colours on.</param>
public sealed record TicketRelatedAssetResponse(
    Guid Id,
    string AssetTag,
    string Name,
    string AssetType,
    string Status)
{
    /// <summary>
    /// Narrows what the contract handed back to what a ticket screen renders, or passes a
    /// missing asset through as missing.
    /// </summary>
    /// <remarks>
    /// The null-in, null-out shape is what lets every call site — the detail read, both
    /// sides of the link response — write one expression instead of a conditional. A ticket
    /// that names no asset and one whose asset has since been soft-deleted both arrive here
    /// as null, and both render as no link, which is what a reader can act on.
    /// </remarks>
    /// <param name="summary">What <c>IAssetLookup</c> returned, or <see langword="null"/>.</param>
    /// <returns>The response shape, or <see langword="null"/>.</returns>
    public static TicketRelatedAssetResponse? From(AssetSummary? summary) =>
        summary is null
            ? null
            : new TicketRelatedAssetResponse(
                summary.Id,
                summary.AssetTag,
                summary.Name,
                summary.AssetType,
                summary.Status);
}

/// <summary>What a ticket looks like immediately after the asset it names changed.</summary>
/// <remarks>
/// Deliberately not the whole ticket, following <see cref="TicketAssignmentResponse"/>: it
/// says only what the link did. Both sides are carried because a screen has to move the
/// row off one asset's support history and onto another's without re-reading either.
/// </remarks>
/// <param name="Id">The ticket.</param>
/// <param name="Number">Its human-readable number, so a toast can name it.</param>
/// <param name="PreviousAsset">The asset it named before, or <see langword="null"/> if it named none.</param>
/// <param name="RelatedAsset">The asset it names now, or <see langword="null"/> after an unlink.</param>
/// <param name="ChangedAt">When the link changed, in UTC.</param>
public sealed record TicketAssetLinkResponse(
    Guid Id,
    string Number,
    TicketRelatedAssetResponse? PreviousAsset,
    TicketRelatedAssetResponse? RelatedAsset,
    DateTimeOffset ChangedAt);

/// <summary>A link change together with the row version the ticket carries after it.</summary>
/// <remarks>
/// Internal and never serialised, for the reason <see cref="TicketDetail"/> gives: the
/// version is a fact about the write, and it travels as an <c>ETag</c> header rather than
/// in the body.
/// </remarks>
/// <param name="Response">The link change as the client sees it.</param>
/// <param name="Version">The <c>xmin</c> row version after the write.</param>
internal sealed record TicketAssetLink(TicketAssetLinkResponse Response, uint Version);
