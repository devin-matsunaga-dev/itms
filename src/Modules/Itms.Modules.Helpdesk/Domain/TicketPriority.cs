namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// How urgent a ticket is — Critical, High, Medium, Low (SPEC.md §2) — together with the
/// response and resolution targets that priority promises.
/// </summary>
/// <remarks>
/// <para>
/// This entity <em>holds</em> the targets; it does not compute against them. Whether a
/// ticket is approaching or has breached its target, and what a spell in Waiting does to
/// the clock, is WP-1.8's arithmetic and lives nowhere in this file.
/// </para>
/// <para>
/// Two identifiers, on purpose. <see cref="Name"/> is what a person reads and an
/// administrator may edit. <see cref="Code"/> is what code reads — a colour lookup, a
/// later rule, an export column — and never changes once the row exists, because
/// everything keyed on it would silently stop matching if it did.
/// </para>
/// <para>
/// Like a category, a priority is retired rather than deleted, so a ticket filed under
/// it stays readable.
/// </para>
/// </remarks>
public sealed class TicketPriority
{
    /// <summary>The longest a priority name may be.</summary>
    public const int NameMaxLength = 64;

    /// <summary>The longest a priority description may be.</summary>
    public const int DescriptionMaxLength = 512;

    /// <summary>The largest response or resolution target, in minutes: 365 days.</summary>
    /// <remarks>
    /// A ceiling rather than a judgement about what a sensible target is. It exists so a
    /// mistyped target cannot become an integer overflow the moment WP-1.8 adds it to a
    /// timestamp.
    /// </remarks>
    public const int MaxTargetMinutes = 365 * 24 * 60;

    private TicketPriority()
    {
        // EF Core materialisation; all three are non-null in the database.
        Code = null!;
        Name = null!;
        NormalizedName = null!;
    }

    /// <summary>The priority's id. What a ticket stores.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// The stable machine identifier — <c>critical</c>, <c>high</c>. Unique, and fixed
    /// for the life of the row: there is deliberately no method that changes it.
    /// </summary>
    public string Code { get; private set; }

    /// <summary>Its display name. Editable.</summary>
    public string Name { get; private set; }

    /// <summary><see cref="Name"/> upper-cased. Carries the uniqueness constraint.</summary>
    public string NormalizedName { get; private set; }

    /// <summary>What this priority is for, or <see langword="null"/>.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Urgency order, lowest first: Critical is 1. Not unique — see
    /// <c>TicketPriorityConfiguration</c> for why, and for the tie-break.
    /// </summary>
    public int Rank { get; private set; }

    /// <summary>Minutes from creation within which a technician should respond.</summary>
    public int ResponseTargetMinutes { get; private set; }

    /// <summary>Minutes from creation within which the ticket should be resolved.</summary>
    public int ResolutionTargetMinutes { get; private set; }

    /// <summary>False once retired. Retired priorities keep resolving for existing tickets.</summary>
    public bool IsActive { get; private set; }

    /// <summary>When the row was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Who created it, or <see langword="null"/> when the system did.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>When the row was last changed (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Who last changed it, or <see langword="null"/> when the system did.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>Creates an active priority.</summary>
    /// <param name="code">Its stable machine identifier. Fixed from here on.</param>
    /// <param name="name">Its display name.</param>
    /// <param name="description">What it is for, or <see langword="null"/>.</param>
    /// <param name="rank">Urgency order, lowest first.</param>
    /// <param name="responseTargetMinutes">Minutes to respond.</param>
    /// <param name="resolutionTargetMinutes">Minutes to resolve. Must not be below the response target.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is creating it, or <see langword="null"/> for the system.</param>
    /// <returns>The new priority, not yet persisted.</returns>
    public static TicketPriority Create(
        string code,
        string name,
        string? description,
        int rank,
        int responseTargetMinutes,
        int resolutionTargetMinutes,
        DateTimeOffset now,
        Guid? actor)
    {
        var trimmedName = ReferenceText.Name(name, NameMaxLength, nameof(name));
        GuardTargets(responseTargetMinutes, resolutionTargetMinutes);

        return new TicketPriority
        {
            // v7 so the primary key is time-ordered and its index does not fragment.
            Id = Guid.CreateVersion7(),
            Code = PriorityCode.Normalize(code, nameof(code)),
            Name = trimmedName,
            NormalizedName = trimmedName.ToUpperInvariant(),
            Description = ReferenceText.Optional(description, DescriptionMaxLength, nameof(description)),
            Rank = rank,
            ResponseTargetMinutes = responseTargetMinutes,
            ResolutionTargetMinutes = resolutionTargetMinutes,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = actor,
            UpdatedAt = now,
            UpdatedBy = actor,
        };
    }

    /// <summary>
    /// Creates a priority with a caller-supplied id, for the reference-data seeder.
    /// </summary>
    /// <remarks>
    /// The seeded rows carry literal ids so they are the same in every database — which
    /// is what makes the seeder idempotent across a rename. Internal, because a generated
    /// id is right for every other caller.
    /// </remarks>
    /// <param name="id">The fixed id.</param>
    /// <param name="code">Its stable machine identifier.</param>
    /// <param name="name">Its display name.</param>
    /// <param name="description">What it is for.</param>
    /// <param name="rank">Urgency order, lowest first.</param>
    /// <param name="responseTargetMinutes">Minutes to respond.</param>
    /// <param name="resolutionTargetMinutes">Minutes to resolve.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The new priority, not yet persisted.</returns>
    internal static TicketPriority Seed(
        Guid id,
        string code,
        string name,
        string description,
        int rank,
        int responseTargetMinutes,
        int resolutionTargetMinutes,
        DateTimeOffset now)
    {
        var priority = Create(
            code, name, description, rank, responseTargetMinutes, resolutionTargetMinutes, now, actor: null);
        priority.Id = id;
        return priority;
    }

    /// <summary>Changes the display name. The code is untouched, so nothing keyed on it moves.</summary>
    /// <param name="name">The new name.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is renaming it.</param>
    public void Rename(string name, DateTimeOffset now, Guid? actor)
    {
        var trimmed = ReferenceText.Name(name, NameMaxLength, nameof(name));
        Name = trimmed;
        NormalizedName = trimmed.ToUpperInvariant();
        Touch(now, actor);
    }

    /// <summary>Replaces the description.</summary>
    /// <param name="description">The new text, or <see langword="null"/> to clear it.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is changing it.</param>
    public void Describe(string? description, DateTimeOffset now, Guid? actor)
    {
        Description = ReferenceText.Optional(description, DescriptionMaxLength, nameof(description));
        Touch(now, actor);
    }

    /// <summary>Moves the priority in the urgency order.</summary>
    /// <param name="rank">Its new rank, lowest being most urgent.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is changing it.</param>
    public void Reorder(int rank, DateTimeOffset now, Guid? actor)
    {
        Rank = rank;
        Touch(now, actor);
    }

    /// <summary>
    /// Sets both SLA targets. They move together because the invariant relates them:
    /// setting one at a time would have to allow a moment in which resolution is due
    /// before response.
    /// </summary>
    /// <param name="responseTargetMinutes">Minutes to respond.</param>
    /// <param name="resolutionTargetMinutes">Minutes to resolve.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is changing them.</param>
    public void SetTargets(
        int responseTargetMinutes,
        int resolutionTargetMinutes,
        DateTimeOffset now,
        Guid? actor)
    {
        GuardTargets(responseTargetMinutes, resolutionTargetMinutes);
        ResponseTargetMinutes = responseTargetMinutes;
        ResolutionTargetMinutes = resolutionTargetMinutes;
        Touch(now, actor);
    }

    /// <summary>Retires the priority. Deletes nothing and breaks no existing ticket.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is retiring it.</param>
    public void Deactivate(DateTimeOffset now, Guid? actor)
    {
        IsActive = false;
        Touch(now, actor);
    }

    /// <summary>Brings a retired priority back into use.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is reinstating it.</param>
    public void Reactivate(DateTimeOffset now, Guid? actor)
    {
        IsActive = true;
        Touch(now, actor);
    }

    private static void GuardTargets(int responseTargetMinutes, int resolutionTargetMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(responseTargetMinutes, 1, nameof(responseTargetMinutes));
        ArgumentOutOfRangeException.ThrowIfLessThan(resolutionTargetMinutes, 1, nameof(resolutionTargetMinutes));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(responseTargetMinutes, MaxTargetMinutes, nameof(responseTargetMinutes));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(resolutionTargetMinutes, MaxTargetMinutes, nameof(resolutionTargetMinutes));

        // A resolution target below the response target would promise the ticket finished
        // before anybody has to have looked at it, and WP-1.8 would compute a breach that
        // no amount of work could avoid.
        ArgumentOutOfRangeException.ThrowIfLessThan(
            resolutionTargetMinutes,
            responseTargetMinutes,
            nameof(resolutionTargetMinutes));
    }

    private void Touch(DateTimeOffset now, Guid? actor)
    {
        UpdatedAt = now;
        UpdatedBy = actor;
    }
}
