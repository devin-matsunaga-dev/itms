namespace Itms.Modules.Assets.Domain;

/// <summary>
/// Where an asset is in its life — In Stock, Deployed, Repair, Retired, Lost, Disposed
/// (SPEC.md §3). Configurable: an operator may rename, reorder, add, and retire, but never
/// change a code.
/// </summary>
/// <remarks>
/// <para>
/// Like <see cref="AssetType"/>, an asset references its status by <see cref="Id"/> and
/// keeps no copy of the name. Unlike a type, it carries an immutable
/// <see cref="Code"/> — see <see cref="AssetStatusCode"/> for why.
/// </para>
/// <para>
/// <b>This entity describes the statuses; it does not move an asset between them.</b>
/// WP-2.2 owns the lifecycle transitions and the asset-history entry ARCHITECTURE.md §11
/// invariant 5 requires of each, which is why <see cref="Asset"/> has no method that
/// changes its status.
/// </para>
/// </remarks>
public sealed class AssetStatus
{
    /// <summary>The longest a status name may be.</summary>
    public const int NameMaxLength = 64;

    /// <summary>The longest a status description may be.</summary>
    public const int DescriptionMaxLength = 512;

    private AssetStatus()
    {
        // EF Core materialisation; all three are non-null in the database.
        Code = null!;
        Name = null!;
        NormalizedName = null!;
    }

    /// <summary>The status's id. What an asset stores.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// The stable machine identifier. Set at creation and never changed — a rename moves
    /// <see cref="Name"/> and leaves this alone.
    /// </summary>
    public string Code { get; private set; }

    /// <summary>Its display name.</summary>
    public string Name { get; private set; }

    /// <summary><see cref="Name"/> upper-cased. Uniqueness is enforced on this.</summary>
    public string NormalizedName { get; private set; }

    /// <summary>What the status means, or <see langword="null"/>.</summary>
    public string? Description { get; private set; }

    /// <summary>Where the status sits in a picker. Not unique — see <c>AssetStatusConfiguration</c>.</summary>
    public int SortOrder { get; private set; }

    /// <summary>False once retired. Retired statuses keep resolving for existing assets.</summary>
    public bool IsActive { get; private set; }

    /// <summary>When the row was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Who created it, or <see langword="null"/> when the system did.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>When the row was last changed (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Who last changed it, or <see langword="null"/> when the system did.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>Creates an active asset status.</summary>
    /// <param name="code">Its stable machine identifier.</param>
    /// <param name="name">Its display name.</param>
    /// <param name="description">What it means, or <see langword="null"/>.</param>
    /// <param name="sortOrder">Where it sits in a picker.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is creating it, or <see langword="null"/> for the system.</param>
    /// <returns>The new status, not yet persisted.</returns>
    public static AssetStatus Create(
        string code,
        string name,
        string? description,
        int sortOrder,
        DateTimeOffset now,
        Guid? actor)
    {
        var trimmed = ReferenceText.Name(name, NameMaxLength, nameof(name));

        return new AssetStatus
        {
            Id = Guid.CreateVersion7(),
            Code = AssetStatusCode.Normalize(code, nameof(code)),
            Name = trimmed,
            NormalizedName = trimmed.ToUpperInvariant(),
            Description = ReferenceText.Optional(description, DescriptionMaxLength, nameof(description)),
            SortOrder = sortOrder,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = actor,
            UpdatedAt = now,
            UpdatedBy = actor,
        };
    }

    /// <summary>Creates a status with a caller-supplied id, for the reference-data seeder.</summary>
    /// <param name="id">The fixed id.</param>
    /// <param name="code">Its stable machine identifier.</param>
    /// <param name="name">Its display name.</param>
    /// <param name="description">What it means.</param>
    /// <param name="sortOrder">Where it sits in a picker.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The new status, not yet persisted.</returns>
    internal static AssetStatus Seed(
        Guid id,
        string code,
        string name,
        string description,
        int sortOrder,
        DateTimeOffset now)
    {
        var status = Create(code, name, description, sortOrder, now, actor: null);
        status.Id = id;
        return status;
    }

    /// <summary>Changes the display name. The code does not move, and every asset follows.</summary>
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

    /// <summary>Moves the status in the picker order.</summary>
    /// <param name="sortOrder">Its new position.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is changing it.</param>
    public void Reorder(int sortOrder, DateTimeOffset now, Guid? actor)
    {
        SortOrder = sortOrder;
        Touch(now, actor);
    }

    /// <summary>Retires the status. Deletes nothing and breaks no existing asset.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is retiring it.</param>
    public void Deactivate(DateTimeOffset now, Guid? actor)
    {
        IsActive = false;
        Touch(now, actor);
    }

    /// <summary>Brings a retired status back into use.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is reinstating it.</param>
    public void Reactivate(DateTimeOffset now, Guid? actor)
    {
        IsActive = true;
        Touch(now, actor);
    }

    private void Touch(DateTimeOffset now, Guid? actor)
    {
        UpdatedAt = now;
        UpdatedBy = actor;
    }
}
