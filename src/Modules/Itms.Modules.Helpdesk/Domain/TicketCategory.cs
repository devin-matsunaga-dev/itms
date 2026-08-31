namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// What a ticket is about — Hardware, Network, Printer, and the rest of the list
/// SPEC.md §2 seeds. Configurable: an operator may rename, reorder, add, and retire.
/// </summary>
/// <remarks>
/// <para>
/// A ticket references a category by <see cref="Id"/> and keeps no copy of its name, so
/// a rename is visible on every existing ticket the moment it commits. That is legal
/// here where it would not be across a module boundary: ARCHITECTURE.md §3 rule 6 bans
/// the foreign key only <em>between</em> modules, and tickets and categories are both
/// Helpdesk's, in one schema.
/// </para>
/// <para>
/// There is no delete. A category is retired instead, exactly as a department is, and
/// for the same reason: retiring keeps every historical ticket's category resolvable
/// while removing it from the pickers. WP-1.2 adds the ticket foreign key with
/// <c>ON DELETE RESTRICT</c> behind it, so the database refuses too.
/// </para>
/// </remarks>
public sealed class TicketCategory
{
    /// <summary>The longest a category name may be.</summary>
    public const int NameMaxLength = 64;

    /// <summary>The longest a category description may be.</summary>
    public const int DescriptionMaxLength = 512;

    private TicketCategory()
    {
        // EF Core materialisation; both are non-null in the database.
        Name = null!;
        NormalizedName = null!;
    }

    /// <summary>The category's id. What a ticket stores.</summary>
    public Guid Id { get; private set; }

    /// <summary>Its display name.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// <see cref="Name"/> upper-cased. Uniqueness is enforced on this, so "Network" and
    /// "network" cannot both exist.
    /// </summary>
    public string NormalizedName { get; private set; }

    /// <summary>What belongs in this category, or <see langword="null"/>.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Where the category sits in a picker. Not unique — see
    /// <c>TicketCategoryConfiguration</c> for why, and for the tie-break.
    /// </summary>
    public int SortOrder { get; private set; }

    /// <summary>False once retired. Retired categories keep resolving for existing tickets.</summary>
    public bool IsActive { get; private set; }

    /// <summary>When the row was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Who created it, or <see langword="null"/> when the system did.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>When the row was last changed (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Who last changed it, or <see langword="null"/> when the system did.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>Creates an active category.</summary>
    /// <param name="name">Its display name.</param>
    /// <param name="description">What belongs in it, or <see langword="null"/>.</param>
    /// <param name="sortOrder">Where it sits in a picker.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is creating it, or <see langword="null"/> for the system.</param>
    /// <returns>The new category, not yet persisted.</returns>
    public static TicketCategory Create(
        string name,
        string? description,
        int sortOrder,
        DateTimeOffset now,
        Guid? actor)
    {
        var trimmed = ReferenceText.Name(name, NameMaxLength, nameof(name));

        return new TicketCategory
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

    /// <summary>
    /// Creates a category with a caller-supplied id, for the reference-data seeder.
    /// </summary>
    /// <remarks>
    /// The seeded rows carry literal ids so they are the same in every database — which
    /// is what makes the seeder idempotent across a rename, and what lets one
    /// environment's ticket be compared with another's. Internal, because a generated id
    /// is right for every other caller.
    /// </remarks>
    /// <param name="id">The fixed id.</param>
    /// <param name="name">Its display name.</param>
    /// <param name="description">What belongs in it.</param>
    /// <param name="sortOrder">Where it sits in a picker.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The new category, not yet persisted.</returns>
    internal static TicketCategory Seed(
        Guid id,
        string name,
        string description,
        int sortOrder,
        DateTimeOffset now)
    {
        var category = Create(name, description, sortOrder, now, actor: null);
        category.Id = id;
        return category;
    }

    /// <summary>Changes the display name. Every ticket already filed under it follows.</summary>
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

    /// <summary>Moves the category in the picker order.</summary>
    /// <param name="sortOrder">Its new position.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is changing it.</param>
    public void Reorder(int sortOrder, DateTimeOffset now, Guid? actor)
    {
        SortOrder = sortOrder;
        Touch(now, actor);
    }

    /// <summary>Retires the category. Deletes nothing and breaks no existing ticket.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is retiring it.</param>
    public void Deactivate(DateTimeOffset now, Guid? actor)
    {
        IsActive = false;
        Touch(now, actor);
    }

    /// <summary>Brings a retired category back into use.</summary>
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
