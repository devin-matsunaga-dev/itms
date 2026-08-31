using System.Text.Json.Serialization;

namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// Where one of a ticket's two SLA clocks stands: SPEC.md §2's "approaching (80%
/// consumed) and breached", plus the three states those two flags only make sense
/// against.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is computed, never stored.</b> Every value here is a comparison between an
/// instant on the ticket row and the instant <c>IClock</c> reports, so a ticket crosses
/// from <see cref="Pending"/> to <see cref="Approaching"/> to <see cref="Breached"/>
/// without anything writing to it. The alternative — a stored flag kept current by a
/// background sweep — would be a column that is wrong between sweeps and a hosted service
/// to operate, for an answer the database can compute in the same query that reads the
/// row.
/// </para>
/// <para>
/// <b>Paused is not one of these.</b> A ticket parked in Waiting still has a state — it
/// may already have breached before it was parked — so "the clock is stopped" is a second
/// fact, carried alongside on <see cref="SlaAssessment.IsPaused"/>, rather than a value
/// that would hide the first.
/// </para>
/// <para>
/// Serialised as text, like <see cref="TicketStatus"/> and
/// <see cref="TicketChangeKind"/> and by the same mechanism: the converter sits on the
/// type so the build-time OpenAPI generator emits a string union rather than an integer
/// whose meaning moves the day somebody reorders this enum.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<SlaState>))]
public enum SlaState
{
    /// <summary>Running, with less than 80% of the target consumed.</summary>
    Pending,

    /// <summary>Running, with at least 80% of the target consumed and the target not yet reached.</summary>
    Approaching,

    /// <summary>
    /// The target was missed: either the clock is still running past its due instant, or
    /// it stopped after it.
    /// </summary>
    Breached,

    /// <summary>The clock stopped at or before its due instant. A response was made, or the ticket was resolved, in time.</summary>
    Met,

    /// <summary>
    /// The ticket was cancelled, so neither target has an outcome.
    /// </summary>
    /// <remarks>
    /// Not <see cref="Met"/> and not <see cref="Breached"/>, at the human's direction: a
    /// ticket abandoned before resolution was never going to be resolved, and there is no
    /// <c>cancelled_at</c> column to freeze an evaluation at. A later package that needs
    /// cancellation to carry an SLA outcome adds that column explicitly rather than
    /// reading <c>updated_at</c>, which moves for unrelated reasons.
    /// </remarks>
    Stopped,
}
