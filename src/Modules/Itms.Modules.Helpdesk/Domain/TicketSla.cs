namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// A ticket's two SLA clocks as they are stored: the targets it was promised, the instants
/// they expire, the 80% marks, and the pause accounting Waiting produces.
/// </summary>
/// <remarks>
/// <para>
/// <b>Instants, not durations, and that is what makes the queue cheap.</b> Every question
/// the system asks about an SLA — is this breached, is it approaching, order the queue by
/// what is due soonest — becomes a comparison between a column and <c>now</c>. A design
/// that stored elapsed time instead would have to recompute every row on every read, and a
/// design that stored a flag would need a background sweep to keep it true.
/// </para>
/// <para>
/// <b>Why the warn instants are stored rather than derived.</b> They could be computed from
/// the due instant and the target on the way out. Storing them means the filter in
/// <c>ListTicketsHandler</c> is a plain <c>timestamptz</c> comparison PostgreSQL can index,
/// rather than interval arithmetic over two columns, and it means the 80% mark cannot drift
/// from the due instant it was derived from when the clock is pushed forward by a pause.
/// </para>
/// <para>
/// <b>The pause moves the resolution clock only.</b> SPEC.md §2 says "Waiting status pauses
/// the resolution clock" and names nothing else, so the response clock runs from creation
/// to the first response without interruption. Time a ticket spends in Resolved before
/// being reopened is not a pause either — that was settled at the human's direction, and a
/// reopened ticket can therefore come back already breached.
/// </para>
/// <para>
/// This is a value read off <see cref="Ticket"/>, not a table of its own and not an EF
/// owned type: the nine fields are plain columns on <c>helpdesk.tickets</c>. It exists so
/// the arithmetic has one shape to work against instead of nine parameters.
/// </para>
/// </remarks>
/// <param name="Targets">What the ticket's priority promised when it was filed.</param>
/// <param name="ResponseDueAt">When the response target expires. Creation plus the response target; never moved by a pause.</param>
/// <param name="ResponseWarnAt">When 80% of the response target is consumed.</param>
/// <param name="RespondedAt">
/// When somebody first answered — the first public comment from anybody but the requester,
/// or the resolution, whichever came first — or <see langword="null"/> while nobody has.
/// Write-once: a reopen does not undo the fact that a response happened.
/// </param>
/// <param name="ResolutionDueAt">
/// When the resolution target expires, including every pause folded in so far. While the
/// ticket is parked in Waiting this reads as the deadline stood when the clock stopped;
/// resuming pushes it forward by however long the pause lasted.
/// </param>
/// <param name="ResolutionWarnAt">When 80% of the resolution target is consumed, moved by a pause exactly as <paramref name="ResolutionDueAt"/> is.</param>
/// <param name="ResolvedAt">When the resolution clock stopped, or <see langword="null"/> while it runs. The ticket's own <see cref="Ticket.ResolvedAt"/>, read here as the clock's stop instant.</param>
/// <param name="PausedAt">When the ticket entered Waiting, or <see langword="null"/> when the clock is running.</param>
/// <param name="PausedTotal">How long the ticket has spent in Waiting across every visit, excluding one in progress.</param>
public readonly record struct TicketSla(
    SlaTargets Targets,
    DateTimeOffset ResponseDueAt,
    DateTimeOffset ResponseWarnAt,
    DateTimeOffset? RespondedAt,
    DateTimeOffset ResolutionDueAt,
    DateTimeOffset ResolutionWarnAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? PausedAt,
    TimeSpan PausedTotal)
{
    /// <summary>Whether the resolution clock is currently parked.</summary>
    public bool IsPaused => PausedAt is not null;

    /// <summary>
    /// The two clocks a ticket starts with.
    /// </summary>
    /// <param name="targets">The targets its priority promises.</param>
    /// <param name="createdAt">When the ticket was raised. Both clocks start here — SPEC.md §2 measures both "against ticket creation".</param>
    /// <returns>The starting state: nothing responded, nothing resolved, nothing paused.</returns>
    public static TicketSla Start(SlaTargets targets, DateTimeOffset createdAt)
    {
        targets.Validate(nameof(targets));

        return new TicketSla(
            targets,
            createdAt + targets.Response,
            SlaClock.WarnPoint(createdAt, targets.Response),
            RespondedAt: null,
            createdAt + targets.Resolution,
            SlaClock.WarnPoint(createdAt, targets.Resolution),
            ResolvedAt: null,
            PausedAt: null,
            TimeSpan.Zero);
    }

    /// <summary>
    /// The same clocks re-cut against <paramref name="targets"/>, as though the ticket had
    /// carried them all along.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what a priority change does to an SLA. Both deadlines are recomputed from
    /// creation rather than from the moment of the change, because SPEC.md §2 measures a
    /// target "against ticket creation" and a ticket raised at nine and promoted to
    /// Critical at eleven was always a Critical ticket raised at nine. Raising the priority
    /// of a ticket that has been open a long time can therefore make it breached
    /// immediately, which is the honest answer.
    /// </para>
    /// <para>
    /// Every pause already folded in survives, because the ticket really did spend that
    /// time Waiting whatever its priority. A pause still in progress is not added here:
    /// it is added when the clock resumes, and until then the deadline is frozen anyway.
    /// </para>
    /// </remarks>
    /// <param name="targets">The new targets.</param>
    /// <param name="createdAt">When the ticket was raised.</param>
    /// <returns>The re-cut clocks, keeping the response, the resolution, and the pause accounting.</returns>
    public TicketSla Retarget(SlaTargets targets, DateTimeOffset createdAt)
    {
        targets.Validate(nameof(targets));

        var resolutionStart = createdAt + PausedTotal;

        return this with
        {
            Targets = targets,
            ResponseDueAt = createdAt + targets.Response,
            ResponseWarnAt = SlaClock.WarnPoint(createdAt, targets.Response),
            ResolutionDueAt = resolutionStart + targets.Resolution,
            ResolutionWarnAt = SlaClock.WarnPoint(resolutionStart, targets.Resolution),
        };
    }

    /// <summary>Stops the resolution clock at <paramref name="pausedAt"/>.</summary>
    /// <param name="pausedAt">When the ticket entered Waiting.</param>
    /// <returns>The paused clocks.</returns>
    public TicketSla Pause(DateTimeOffset pausedAt) => this with { PausedAt = pausedAt };

    /// <summary>
    /// Restarts the resolution clock, pushing both of its instants forward by however long
    /// the pause lasted.
    /// </summary>
    /// <remarks>
    /// Moving the deadlines rather than accumulating a debt to subtract later is what keeps
    /// the queue a set of column comparisons. A pause that somehow ran backwards — a clock
    /// adjusted under a running ticket — contributes nothing rather than pulling the
    /// deadline in.
    /// </remarks>
    /// <param name="resumedAt">When the ticket left Waiting.</param>
    /// <returns>The running clocks, or the same clocks unchanged if none was paused.</returns>
    public TicketSla Resume(DateTimeOffset resumedAt)
    {
        if (PausedAt is not { } pausedAt)
        {
            return this;
        }

        var elapsed = resumedAt - pausedAt;

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return this with
        {
            PausedAt = null,
            PausedTotal = PausedTotal + elapsed,
            ResolutionDueAt = ResolutionDueAt + elapsed,
            ResolutionWarnAt = ResolutionWarnAt + elapsed,
        };
    }

    /// <summary>Records the first response, if none has been recorded.</summary>
    /// <remarks>
    /// Write-once. A second call is ignored rather than refused: the callers are a comment
    /// being posted and a ticket being resolved, and neither should fail because the other
    /// happened first.
    /// </remarks>
    /// <param name="respondedAt">When the response was made.</param>
    /// <returns>The clocks, with the response recorded if it was the first.</returns>
    public TicketSla Respond(DateTimeOffset respondedAt) =>
        RespondedAt is null ? this with { RespondedAt = respondedAt } : this;

    /// <summary>Records when the resolution clock stopped, or that it is running again.</summary>
    /// <param name="resolvedAt">When the ticket was resolved, or <see langword="null"/> when it has been reopened.</param>
    /// <returns>The clocks, with the resolution instant set or cleared.</returns>
    public TicketSla Resolved(DateTimeOffset? resolvedAt) => this with { ResolvedAt = resolvedAt };
}
