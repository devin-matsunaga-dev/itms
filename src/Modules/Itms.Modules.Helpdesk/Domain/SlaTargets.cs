namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// The two promises a priority makes about a ticket: how long until somebody answers it,
/// and how long until it is fixed (SPEC.md §2, "per-priority response and resolution
/// targets").
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the targets travel rather than being read back off the priority.</b> A priority's
/// targets are editable — <see cref="TicketPriority.SetTargets"/> exists and WP-5.8 will
/// put a screen on it — and a ticket has to keep the promise that was made when it was
/// filed. So the numbers are copied onto the ticket at creation, the same shape §3 rule 6
/// uses for a display name: the id says which priority, the copy says what it said at the
/// time. Editing a priority's targets moves the tickets filed after the edit and no
/// others.
/// </para>
/// <para>
/// Minutes rather than a <see cref="TimeSpan"/> on the wire and in the column, because
/// that is what <see cref="TicketPriority"/> holds and what an administrator types. The
/// <see cref="TimeSpan"/> properties are the arithmetic's view of the same two numbers.
/// </para>
/// </remarks>
/// <param name="ResponseMinutes">Minutes from creation within which a technician should respond.</param>
/// <param name="ResolutionMinutes">Minutes from creation — excluding time spent Waiting — within which the ticket should be resolved.</param>
public readonly record struct SlaTargets(int ResponseMinutes, int ResolutionMinutes)
{
    /// <summary>The targets <paramref name="priority"/> currently promises.</summary>
    /// <param name="priority">The priority the ticket is being filed at.</param>
    /// <returns>Its two targets, as they read right now.</returns>
    public static SlaTargets Of(TicketPriority priority)
    {
        ArgumentNullException.ThrowIfNull(priority);

        return new SlaTargets(priority.ResponseTargetMinutes, priority.ResolutionTargetMinutes);
    }

    /// <summary>How long a response has, as a span.</summary>
    public TimeSpan Response => TimeSpan.FromMinutes(ResponseMinutes);

    /// <summary>How long a resolution has, as a span, before any pause is added.</summary>
    public TimeSpan Resolution => TimeSpan.FromMinutes(ResolutionMinutes);

    /// <summary>
    /// Checks the pair is one a clock can be run against.
    /// </summary>
    /// <remarks>
    /// These are <see cref="TicketPriority"/>'s own rules, checked a second time on the way
    /// onto a ticket. Not defensive habit: the numbers reach here off a database row that
    /// could have been written before a rule existed, and a zero or a negative target would
    /// produce a ticket due before it was raised — a breach nothing could ever avoid.
    /// </remarks>
    /// <param name="parameterName">The caller's parameter name, for the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either target is out of range, or resolution is shorter than response.</exception>
    public void Validate(string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ResponseMinutes, 1, parameterName);
        ArgumentOutOfRangeException.ThrowIfLessThan(ResolutionMinutes, 1, parameterName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ResponseMinutes, TicketPriority.MaxTargetMinutes, parameterName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ResolutionMinutes, TicketPriority.MaxTargetMinutes, parameterName);
        ArgumentOutOfRangeException.ThrowIfLessThan(ResolutionMinutes, ResponseMinutes, parameterName);
    }
}
