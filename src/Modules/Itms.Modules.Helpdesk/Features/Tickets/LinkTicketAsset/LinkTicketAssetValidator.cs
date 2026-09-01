using FluentValidation;

namespace Itms.Modules.Helpdesk.Features.Tickets.LinkTicketAsset;

/// <summary>Checks the shape of a link request before the handler runs.</summary>
/// <remarks>
/// As little as <c>AssignTicketValidator</c> can decide, and for the same reason: whether
/// the asset exists is a fact about another module's rows, answered by the handler through
/// <c>IAssetLookup</c>, and whether the ticket may be relinked at all depends on the state
/// it is in, answered by <see cref="Domain.Ticket.LinkAsset"/>.
/// </remarks>
public sealed class LinkTicketAssetValidator : AbstractValidator<LinkTicketAssetRequest>
{
    /// <summary>Builds the rules.</summary>
    public LinkTicketAssetValidator() =>
        // An omitted assetId means "unlink" and is the null. An explicitly empty Guid
        // means the client built the request wrong, and reading it as an unlink would
        // clear a link nobody asked to clear.
        RuleFor(request => request.AssetId)
            .NotEqual(Guid.Empty)
            .WithMessage("Choose an asset to link, or omit the field to clear the link.");
}
