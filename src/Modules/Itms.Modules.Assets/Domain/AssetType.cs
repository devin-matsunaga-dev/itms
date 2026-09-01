namespace Itms.Modules.Assets.Domain;

/// <summary>
/// What kind of thing an asset is — desktop, laptop, switch, printer, and the rest of the
/// list SPEC.md §3 seeds. Configurable: an operator may rename, reorder, add, and retire.
/// </summary>
/// <remarks>
/// <para>
/// An asset references its type by <see cref="Id"/> and keeps no copy of the name, so a
/// rename is visible on every existing asset the moment it commits. That is legal here
/// where it would not be across a module boundary: ARCHITECTURE.md §3 rule 6 bans the
/// foreign key only <em>between</em> modules, and assets and their types are both this
/// module's, in one schema.
/// </para>
/// <para>
/// There is no delete, and no code. Retirement is the removal path, exactly as it is for a
/// ticket category and for the same reason — a retired type keeps every historical asset's
/// classification resolvable while removing it from the pickers, and
/// <c>ON DELETE RESTRICT</c> on <c>assets.asset_type_id</c> makes the database refuse too.
/// A stable machine identifier was deliberately not given: nothing keys off "Laptop", where
/// <see cref="AssetStatus"/> drives lifecycle rules and a colour and therefore needs one.
/// If an integration ever has to name a type stably, adding a code is a migration plus a
/// nullable column, not a redesign — which is the call WP-1.1 made for a ticket category.
/// </para>
/// </remarks>
public sealed class AssetType
{
    /// <summary>The longest a type name may be.</summary>
    public const int NameMaxLength = 64;

    /// <summary>The longest a type description may be.</summary>
    public const int DescriptionMaxLength = 512;

    private AssetType()
    {
        // EF Core materialisation; both are non-null in the database.
        Name = null!;
        NormalizedName = null!;
    }

    /// <summary>The type's id. What an asset stores.</summary>
    public Guid Id { get; private set; }

    /// <summary>Its display name.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// <see cref="Name"/> upper-cased. Uniqueness is enforced on this, so "Laptop" and
    /// "laptop" cannot both exist.
    /// </summary>
    public string NormalizedName { get; private set; }

    /// <summary>What belongs in this type, or <see langword="null"/>.</summary>
    public string? Description { get; private set; }

    /// <summary>Where the type sits in a picker. Not unique — see <c>AssetTypeConfiguration</c>.</summary>
    public int SortOrder { get; private set; }

    /// <summary>False once retired. Retired types keep resolving for existing assets.</summary>
    public bool IsActive { get; private set; }

    /// <summary>When the row was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Who created it, or <see langword="null"/> when the system did.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>When the row was last changed (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Who last changed it, or <see langword="null"/> when the system did.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>Creates an active asset type.</summary>
    /// <param name="name">Its display name.</param>
    /// <param name="description">What belongs in it, or <see langword="null"/>.</param>
    /// <param name="sortOrder">Where it sits in a picker.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is creating it, or <see langword="null"/> for the system.</param>
    /// <returns>The new type, not yet persisted.</returns>
    public static AssetType Create(
        string name,
        string? description,
        int sortOrder,
        DateTimeOffset now,
        Guid? actor)
    {
        var trimmed = ReferenceText.Name(name, NameMaxLength, nameof(name));

        return new AssetType
        {
            // v7 so the primary key is time-ordered and its index does not fragment.
            Id = Guid.CreateVersion7(),
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

    /// <summary>Creates a type with a caller-supplied id, for the reference-data seeder.</summary>
    /// <remarks>
    /// The seeded rows carry literal ids so they are the same in every database — which is
    /// what makes the seeder idempotent across a rename. Internal, because a generated id is
    /// right for every other caller.
    /// </remarks>
    /// <param name="id">The fixed id.</param>
    /// <param name="name">Its display name.</param>
    /// <param name="description">What belongs in it.</param>
    /// <param name="sortOrder">Where it sits in a picker.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The new type, not yet persisted.</returns>
    internal static AssetType Seed(
        Guid id,
        string name,
        string description,
        int sortOrder,
        DateTimeOffset now)
    {
        var type = Create(name, description, sortOrder, now, actor: null);
        type.Id = id;
        return type;
    }

    /// <summary>Changes the display name. Every asset already classified under it follows.</summary>
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

    /// <summary>Moves the type in the picker order.</summary>
    /// <param name="sortOrder">Its new position.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is changing it.</param>
    public void Reorder(int sortOrder, DateTimeOffset now, Guid? actor)
    {
        SortOrder = sortOrder;
        Touch(now, actor);
    }

    /// <summary>Retires the type. Deletes nothing and breaks no existing asset.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is retiring it.</param>
    public void Deactivate(DateTimeOffset now, Guid? actor)
    {
        IsActive = false;
        Touch(now, actor);
    }

    /// <summary>Brings a retired type back into use.</summary>
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
