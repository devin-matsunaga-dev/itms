using Itms.Modules.Assets.Domain;

namespace Itms.Modules.Assets.Features.AssetHistory;

/// <summary>One line of an asset's timeline, as the API renders it.</summary>
/// <remarks>
/// <para>
/// The values are the display text they were at the time, not ids — see
/// <see cref="AssetHistoryEntry"/> for why. A client renders them; it must not try to
/// resolve them back to a row.
/// </para>
/// <para>
/// The projection lives on the response type rather than inside the list handler so that
/// WP-2.6's detail screen, which shows the same timeline beside the asset, renders one
/// shape rather than inventing a second.
/// </para>
/// </remarks>
/// <param name="Id">The entry's id.</param>
/// <param name="Kind">Which dimension moved.</param>
/// <param name="FromValue">What it read before, or <see langword="null"/> when there was nothing there.</param>
/// <param name="ToValue">What it reads now, or <see langword="null"/> when the change cleared it.</param>
/// <param name="Note">What the operator said about it, or <see langword="null"/>.</param>
/// <param name="OccurredAt">When the change happened (UTC).</param>
/// <param name="Sequence">
/// Where this line sits among the lines the same operation wrote. Entries sharing an
/// <paramref name="OccurredAt"/> came from one operation and are meant to be read together.
/// </param>
/// <param name="ActorId">Who made it, or <see langword="null"/> when the system did.</param>
/// <param name="ActorName">Their display name at the time, or <see langword="null"/>.</param>
public sealed record AssetHistoryEntryResponse(
    Guid Id,
    AssetChangeKind Kind,
    string? FromValue,
    string? ToValue,
    string? Note,
    DateTimeOffset OccurredAt,
    int Sequence,
    Guid? ActorId,
    string? ActorName)
{
    /// <summary>The projection every history query uses, so one shape is built in one place.</summary>
    internal static System.Linq.Expressions.Expression<Func<AssetHistoryEntry, AssetHistoryEntryResponse>> Projection() =>
        entry => new AssetHistoryEntryResponse(
            entry.Id,
            entry.Kind,
            entry.FromValue,
            entry.ToValue,
            entry.Note,
            entry.OccurredAt,
            entry.Sequence,
            entry.ActorId,
            entry.ActorName);
}
