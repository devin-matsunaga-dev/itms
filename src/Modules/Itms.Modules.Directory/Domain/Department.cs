namespace Itms.Modules.Directory.Domain;

/// <summary>
/// A department of the organisation. Tickets, users, assets, and reporting all reference
/// these rows (SPEC.md §4).
/// </summary>
/// <remarks>
/// A department is retired rather than deleted. <c>DepartmentSummary.IsActive</c> is
/// documented as "false once retired; existing tickets and users keep referencing it",
/// and Directory cannot see whether another module still points at a row — §3 rule 6
/// keeps those references as plain identifiers with no foreign key, so the database
/// cannot answer the question either. Retiring keeps every historical reference
/// resolvable, which deleting would not.
/// </remarks>
public sealed class Department
{
    /// <summary>The longest a department name may be.</summary>
    public const int NameMaxLength = 128;

    /// <summary>The longest a department code may be.</summary>
    public const int CodeMaxLength = 32;

    /// <summary>The longest a department description may be.</summary>
    public const int DescriptionMaxLength = 512;

    private Department()
    {
        // EF Core materialisation; both are non-null in the database.
        Name = null!;
        NormalizedName = null!;
    }

    /// <summary>The department's id.</summary>
    public Guid Id { get; private set; }

    /// <summary>Its display name.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// <see cref="Name"/> upper-cased. Uniqueness is enforced on this, so "Finance" and
    /// "finance" cannot both exist.
    /// </summary>
    public string NormalizedName { get; private set; }

    /// <summary>A short code such as <c>FIN</c>, or <see langword="null"/>. Unique when present.</summary>
    public string? Code { get; private set; }

    /// <summary><see cref="Code"/> upper-cased, or <see langword="null"/>. Carries the uniqueness constraint.</summary>
    public string? NormalizedCode { get; private set; }

    /// <summary>Free text about the department, or <see langword="null"/>.</summary>
    public string? Description { get; private set; }

    /// <summary>False once retired. Retired departments keep resolving for existing references.</summary>
    public bool IsActive { get; private set; }

    /// <summary>When the row was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Who created it, or <see langword="null"/> when the system did.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>When the row was last changed (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Who last changed it, or <see langword="null"/> when the system did.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>Creates an active department.</summary>
    /// <param name="name">Its display name.</param>
    /// <param name="code">A short code, or <see langword="null"/>.</param>
    /// <param name="description">Free text, or <see langword="null"/>.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is creating it, or <see langword="null"/> for the system.</param>
    /// <returns>The new department, not yet persisted.</returns>
    public static Department Create(
        string name,
        string? code,
        string? description,
        DateTimeOffset now,
        Guid? actor)
    {
        var trimmedName = NormalizeName(name);
        var trimmedCode = NormalizeCode(code);

        return new Department
        {
            // v7 so the primary key is time-ordered and its index does not fragment.
            Id = Guid.CreateVersion7(),
            Name = trimmedName,
            NormalizedName = trimmedName.ToUpperInvariant(),
            Code = trimmedCode,
            NormalizedCode = trimmedCode?.ToUpperInvariant(),
            Description = NormalizeDescription(description),
            IsActive = true,
            CreatedAt = now,
            CreatedBy = actor,
            UpdatedAt = now,
            UpdatedBy = actor,
        };
    }

    /// <summary>Changes the display name.</summary>
    /// <param name="name">The new name.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is renaming it.</param>
    public void Rename(string name, DateTimeOffset now, Guid? actor)
    {
        var trimmed = NormalizeName(name);
        Name = trimmed;
        NormalizedName = trimmed.ToUpperInvariant();
        Touch(now, actor);
    }

    /// <summary>Sets or clears the short code.</summary>
    /// <param name="code">The new code, or <see langword="null"/> to clear it.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is changing it.</param>
    public void SetCode(string? code, DateTimeOffset now, Guid? actor)
    {
        var trimmed = NormalizeCode(code);
        Code = trimmed;
        NormalizedCode = trimmed?.ToUpperInvariant();
        Touch(now, actor);
    }

    /// <summary>Replaces the free text.</summary>
    /// <param name="description">The new description, or <see langword="null"/> to clear it.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is changing it.</param>
    public void Describe(string? description, DateTimeOffset now, Guid? actor)
    {
        Description = NormalizeDescription(description);
        Touch(now, actor);
    }

    /// <summary>Retires the department. Deletes nothing and breaks no existing reference.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is retiring it.</param>
    public void Deactivate(DateTimeOffset now, Guid? actor)
    {
        IsActive = false;
        Touch(now, actor);
    }

    /// <summary>Brings a retired department back into use.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is reinstating it.</param>
    public void Reactivate(DateTimeOffset now, Guid? actor)
    {
        IsActive = true;
        Touch(now, actor);
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();

        return trimmed.Length <= NameMaxLength
            ? trimmed
            : throw new ArgumentException($"A department name may be at most {NameMaxLength} characters.", nameof(name));
    }

    private static string? NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var trimmed = code.Trim();

        return trimmed.Length <= CodeMaxLength
            ? trimmed
            : throw new ArgumentException($"A department code may be at most {CodeMaxLength} characters.", nameof(code));
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();

        return trimmed.Length <= DescriptionMaxLength
            ? trimmed
            : throw new ArgumentException($"A department description may be at most {DescriptionMaxLength} characters.", nameof(description));
    }

    private void Touch(DateTimeOffset now, Guid? actor)
    {
        UpdatedAt = now;
        UpdatedBy = actor;
    }
}
