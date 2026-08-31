using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.TicketHistory;

/// <summary>One line of a ticket's timeline, as the API renders it.</summary>
/// <remarks>
/// <para>
/// The values are the display text they were at the time, not ids — see
/// <see cref="TicketHistoryEntry"/> for why. A client renders them; it must not try to
/// resolve them back to a row.
/// </para>
/// <para>
/// WP-1.5's ticket detail is expected to carry this same shape rather than invent a second
/// one, which is why the projection lives on the response type instead of inside the list
/// handler.
/// </para>
/// </remarks>
/// <param name="Id">The entry's id.</param>
/// <param name="Kind">Which dimension moved.</param>
/// <param name="FromValue">What it read before, or <see langword="null"/> when there was nothing there.</param>
/// <param name="ToValue">What it reads now, or <see langword="null"/> when the change cleared it.</param>
/// <param name="OccurredAt">When the change happened (UTC).</param>
/// <param name="Sequence">
/// Where this line sits among the lines the same change wrote. Entries sharing an
/// <paramref name="OccurredAt"/> came from one change and are meant to be read together.
/// </param>
/// <param name="ActorId">Who made it, or <see langword="null"/> when the system did.</param>
/// <param name="ActorName">Their display name at the time, or <see langword="null"/>.</param>
public sealed record TicketHistoryEntryResponse(
    Guid Id,
    TicketChangeKind Kind,
    string? FromValue,
    string? ToValue,
    DateTimeOffset OccurredAt,
    int Sequence,
    Guid? ActorId,
    string? ActorName)
{
    /// <summary>The projection every history query uses, so one shape is built in one place.</summary>
    internal static System.Linq.Expressions.Expression<Func<TicketHistoryEntry, TicketHistoryEntryResponse>> Projection() =>
        entry => new TicketHistoryEntryResponse(
            entry.Id,
            entry.Kind,
            entry.FromValue,
            entry.ToValue,
            entry.OccurredAt,
            entry.Sequence,
            entry.ActorId,
            entry.ActorName);
}
