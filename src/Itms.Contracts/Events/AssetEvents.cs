namespace Itms.Contracts.Events;

/// <summary>
/// An asset was assigned to a user, or returned from one. ARCHITECTURE.md §11
/// invariant 5 requires an asset-history entry for this; the history is written by
/// Assets in the same transaction, and this event is what lets Notifications and
/// Audit react without Assets knowing they exist.
/// </summary>
/// <param name="AssetId">The asset.</param>
/// <param name="AssetTag">The immutable asset tag, carried so consumers can render it without a lookup.</param>
/// <param name="AssignedToUserId">The user now holding it, or <see langword="null"/> when it was returned to stock.</param>
/// <param name="PreviousUserId">Who held it before, if anyone.</param>
public sealed record AssetAssigned(
    Guid AssetId,
    string AssetTag,
    Guid? AssignedToUserId,
    Guid? PreviousUserId) : DomainEvent;

/// <summary>
/// An asset's lifecycle status changed — in service, in repair, retired.
/// </summary>
/// <param name="AssetId">The asset.</param>
/// <param name="AssetTag">The immutable asset tag.</param>
/// <param name="FromStatus">The status before the change.</param>
/// <param name="ToStatus">The status after the change.</param>
public sealed record AssetStatusChanged(
    Guid AssetId,
    string AssetTag,
    string FromStatus,
    string ToStatus) : DomainEvent;
