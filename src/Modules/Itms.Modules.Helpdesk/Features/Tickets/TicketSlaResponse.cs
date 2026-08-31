using System.Text.Json.Serialization;
using Itms.Modules.Helpdesk.Domain;

namespace Itms.Modules.Helpdesk.Features.Tickets;

/// <summary>
/// A ticket's two SLA clocks as the API renders them: what was promised, when each
/// deadline falls, and where each clock stands right now.
/// </summary>
/// <remarks>
/// <para>
/// <b>The instants and the states both travel, and a client needs both.</b> The states are
/// computed against <c>IClock</c> at the moment the response is built, so a ticket a minute
/// from breaching says <c>Approaching</c> and will still say it on a screen left open for
/// an hour. The deadlines are absolute, so a client that wants a live countdown — or wants
/// to re-colour a row without another round trip — has what it needs to work it out for
/// itself.
/// </para>
/// <para>
/// <b>Every field here is either a column or a comparison between two of them.</b> The
/// record is built inside the list and detail projections, straight from
/// <c>helpdesk.tickets</c>, and <see cref="Assessed"/> fills in the three computed members
/// afterwards. Nothing is looked up per row.
/// </para>
/// </remarks>
/// <param name="ResponseTargetMinutes">Minutes the priority allowed for a response when the ticket was filed.</param>
/// <param name="ResponseDueAt">When the response target expires. A pause never moves it.</param>
/// <param name="ResponseWarnAt">When 80% of the response target is consumed.</param>
/// <param name="RespondedAt">
/// When somebody first answered — the first public comment from anybody but the requester,
/// or the resolution — or <see langword="null"/> while nobody has.
/// </param>
/// <param name="ResolutionTargetMinutes">Minutes the priority allowed for a resolution when the ticket was filed.</param>
/// <param name="ResolutionDueAt">
/// When the resolution target expires, every pause so far included. The same instant the
/// ticket's own <c>dueAt</c> carries; it is repeated here so the SLA object reads on its
/// own.
/// </param>
/// <param name="ResolutionWarnAt">When 80% of the resolution target is consumed.</param>
/// <param name="ResolvedAt">
/// When the resolution clock stopped, or <see langword="null"/> while it runs. The ticket's
/// own resolution instant, seen from the SLA's side.
/// </param>
/// <param name="PausedAt">When the ticket entered Waiting, or <see langword="null"/> when the clock is running.</param>
/// <param name="PausedTotal">
/// How long the ticket has spent Waiting across every visit, excluding one in progress. Not
/// serialised — <see cref="PausedSeconds"/> is what goes on the wire, because nothing else
/// in this API sends a duration and a client should not have to parse one.
/// </param>
public sealed record TicketSlaResponse(
    int ResponseTargetMinutes,
    DateTimeOffset ResponseDueAt,
    DateTimeOffset ResponseWarnAt,
    DateTimeOffset? RespondedAt,
    int ResolutionTargetMinutes,
    DateTimeOffset ResolutionDueAt,
    DateTimeOffset ResolutionWarnAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? PausedAt,
    [property: JsonIgnore] TimeSpan PausedTotal)
{
    /// <summary>Where the response clock stands. Filled by <see cref="Assessed"/>.</summary>
    public SlaState ResponseState { get; init; }

    /// <summary>Where the resolution clock stands. Filled by <see cref="Assessed"/>.</summary>
    public SlaState ResolutionState { get; init; }

    /// <summary>
    /// Whether the resolution clock is parked because the ticket is Waiting.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="ResolutionState"/> on purpose: a ticket can be parked
    /// <em>and</em> already breached, and a state of "paused" would hide the breach.
    /// </remarks>
    public bool IsPaused { get; init; }

    /// <summary>How long the ticket has spent Waiting in total, in whole seconds.</summary>
    /// <remarks>
    /// Seconds, and an integer: a <see cref="TimeSpan"/> would reach the client as a string
    /// it has to parse, and a fractional number reaches it as OpenAPI's <c>number | string</c>
    /// union — because a .NET <c>double</c> can serialise as <c>"NaN"</c>. A count of
    /// seconds is a plain integer on the wire and precise enough for anything a screen
    /// shows.
    /// </remarks>
    public long PausedSeconds => (long)PausedTotal.TotalSeconds;

    /// <summary>
    /// The same clocks with the three computed members filled in for
    /// <paramref name="now"/>.
    /// </summary>
    /// <remarks>
    /// Runs in memory, after the projection: it is the one place the wire shape and the
    /// domain arithmetic meet, so a screen and a report can never disagree about what
    /// "breached" means. The rule itself lives in <see cref="SlaAssessment"/> and is not
    /// restated here.
    /// </remarks>
    /// <param name="status">Where the ticket sits in the workflow — a cancelled one has no outcome.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <returns>The assessed clocks.</returns>
    public TicketSlaResponse Assessed(TicketStatus status, DateTimeOffset now)
    {
        var assessment = SlaAssessment.Of(ToDomain(), status, now);

        return this with
        {
            ResponseState = assessment.Response,
            ResolutionState = assessment.Resolution,
            IsPaused = assessment.IsPaused,
        };
    }

    /// <summary>The domain shape of the same nine stored values.</summary>
    /// <returns>The clocks, for the arithmetic to read.</returns>
    private TicketSla ToDomain() => new(
        new SlaTargets(ResponseTargetMinutes, ResolutionTargetMinutes),
        ResponseDueAt,
        ResponseWarnAt,
        RespondedAt,
        ResolutionDueAt,
        ResolutionWarnAt,
        ResolvedAt,
        PausedAt,
        PausedTotal);
}
