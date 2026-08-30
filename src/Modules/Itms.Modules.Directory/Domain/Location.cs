namespace Itms.Modules.Directory.Domain;

/// <summary>
/// One node of the physical location tree: an organisation, site, building, floor or
/// area, or room (SPEC.md §5).
/// </summary>
/// <remarks>
/// <para>
/// The tree is self-referencing, and every node carries two materialised paths so that
/// reading a location never walks its ancestors. <see cref="Path"/> is a path of ids and
/// is what subtree queries match on; <see cref="FullPath"/> is the display text
/// <c>ILocationLookup</c> hands to other modules. A node's full path therefore costs one
/// row read rather than one query per level.
/// </para>
/// <para>
/// The cost of materialising is that a rename or a move has to rewrite the subtree.
/// That is deliberate: renames are rare and reads are constant, and the rewrite is a
/// single prefix <c>UPDATE</c> (see <see cref="SubtreeRewrite"/>).
/// </para>
/// </remarks>
public sealed class Location
{
    /// <summary>What separates the levels of <see cref="FullPath"/>.</summary>
    /// <remarks>
    /// A slash rather than the arrow SPEC.md draws the hierarchy with, because this
    /// value is rendered in table cells and written into CSV exports, and an arrow
    /// survives neither reliably.
    /// </remarks>
    public const string PathSeparator = " / ";

    /// <summary>The longest a location's own name may be.</summary>
    public const int NameMaxLength = 128;

    /// <summary>The longest a location's description may be.</summary>
    public const int DescriptionMaxLength = 512;

    private Location()
    {
        // EF Core materialisation. These four are non-null in the database; the
        // null-forgiving assignments are what tell the compiler so.
        Name = null!;
        NormalizedName = null!;
        Path = null!;
        FullPath = null!;
    }

    /// <summary>The node's id.</summary>
    public Guid Id { get; private set; }

    /// <summary>The node's own name — "Room G-04", not the whole path.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// <see cref="Name"/> upper-cased, so "room g-04" and "Room G-04" cannot both exist
    /// under one parent. Uniqueness is enforced on this rather than on an expression
    /// index, which keeps the constraint visible in the model.
    /// </summary>
    public string NormalizedName { get; private set; }

    /// <summary>Which level of the hierarchy this node is.</summary>
    public LocationKind Kind { get; private set; }

    /// <summary>The parent node, or <see langword="null"/> at the root.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>
    /// The materialised path of ids from the root to and including this node, as
    /// <c>/{id}/{id}/</c>. Descendants of a node are exactly the rows whose path starts
    /// with that node's path.
    /// </summary>
    public string Path { get; private set; }

    /// <summary>
    /// The display path from the root to and including this node, joined with
    /// <see cref="PathSeparator"/>. This is what <c>LocationSummary.Path</c> carries.
    /// </summary>
    public string FullPath { get; private set; }

    /// <summary>How far below the root this node sits. Zero at the root.</summary>
    public int Depth { get; private set; }

    /// <summary>Free text about the node, or <see langword="null"/>.</summary>
    public string? Description { get; private set; }

    /// <summary>When the row was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Who created it, or <see langword="null"/> when the system did — seeding, for instance.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>When the row was last changed (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Who last changed it, or <see langword="null"/> when the system did.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>
    /// Creates a node under <paramref name="parent"/>, or a root when it is
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="parent">The parent node, or <see langword="null"/> for a root.</param>
    /// <param name="name">The node's own name.</param>
    /// <param name="kind">Which level of the hierarchy it is.</param>
    /// <param name="description">Free text, or <see langword="null"/>.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is creating it, or <see langword="null"/> for the system.</param>
    /// <returns>The new node, not yet persisted.</returns>
    /// <exception cref="InvalidOperationException">
    /// The placement breaks the hierarchy. Handlers check <see cref="CanAdopt"/> first and
    /// return a 409; reaching this throw means a caller skipped that check.
    /// </exception>
    public static Location Create(
        Location? parent,
        string name,
        LocationKind kind,
        string? description,
        DateTimeOffset now,
        Guid? actor)
    {
        var trimmed = Normalize(name);

        if (parent is null)
        {
            if (!LocationHierarchy.CanBeRoot(kind))
            {
                throw new InvalidOperationException($"A root location must be an {LocationHierarchy.RootKind}, not a {kind}.");
            }
        }
        else if (!parent.CanAdopt(kind))
        {
            throw new InvalidOperationException($"A {kind} cannot sit under a {parent.Kind}.");
        }

        var id = Guid.CreateVersion7();

        return new Location
        {
            Id = id,
            Name = trimmed,
            NormalizedName = trimmed.ToUpperInvariant(),
            Kind = kind,
            ParentId = parent?.Id,
            Path = ComposePath(parent?.Path, id),
            FullPath = ComposeFullPath(parent?.FullPath, trimmed),
            Depth = parent is null ? 0 : parent.Depth + 1,
            Description = NormalizeDescription(description),
            CreatedAt = now,
            CreatedBy = actor,
            UpdatedAt = now,
            UpdatedBy = actor,
        };
    }

    /// <summary>
    /// Whether a node of <paramref name="childKind"/> may be created directly under this
    /// one, with room left before <see cref="LocationHierarchy.MaxDepth"/>.
    /// </summary>
    /// <param name="childKind">The prospective child's kind.</param>
    public bool CanAdopt(LocationKind childKind) =>
        LocationHierarchy.CanContain(Kind, childKind) && Depth + 1 < LocationHierarchy.MaxDepth;

    /// <summary>Whether <paramref name="other"/> lies somewhere beneath this node.</summary>
    /// <param name="other">The node to test.</param>
    /// <remarks>
    /// Answered from the materialised path, so "is this a descendant" costs no query —
    /// which is what makes the cycle check on a move cheap enough to always run.
    /// </remarks>
    public bool IsAncestorOf(Location other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.Id != Id && other.Path.StartsWith(Path, StringComparison.Ordinal);
    }

    /// <summary>
    /// Renames the node and recomputes its display path.
    /// </summary>
    /// <param name="name">The new name.</param>
    /// <param name="parentFullPath">The parent's <see cref="FullPath"/>, or <see langword="null"/> at the root.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is renaming it.</param>
    /// <returns>What the subtree beneath this node now needs.</returns>
    public SubtreeRewrite Rename(string name, string? parentFullPath, DateTimeOffset now, Guid? actor)
    {
        var trimmed = Normalize(name);
        var oldFullPath = FullPath;

        Name = trimmed;
        NormalizedName = trimmed.ToUpperInvariant();
        FullPath = ComposeFullPath(parentFullPath, trimmed);
        Touch(now, actor);

        // A rename changes no id, so the id path — and therefore which rows are
        // descendants — is untouched.
        return new SubtreeRewrite(Path, Path, oldFullPath, FullPath, DepthShift: 0);
    }

    /// <summary>Replaces the node's free text.</summary>
    /// <param name="description">The new description, or <see langword="null"/> to clear it.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is changing it.</param>
    public void Describe(string? description, DateTimeOffset now, Guid? actor)
    {
        Description = NormalizeDescription(description);
        Touch(now, actor);
    }

    /// <summary>
    /// Reparents the node, recomputing its id path, display path, and depth.
    /// </summary>
    /// <param name="newParent">The new parent, or <see langword="null"/> to move it to the root.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="actor">Who is moving it.</param>
    /// <returns>What the subtree beneath this node now needs.</returns>
    /// <exception cref="InvalidOperationException">
    /// The move breaks the hierarchy or would make the node its own ancestor. Handlers
    /// check <see cref="CanAdopt"/> and <see cref="IsAncestorOf"/> first and return a 409;
    /// reaching this throw means a caller skipped those checks.
    /// </exception>
    public SubtreeRewrite MoveTo(Location? newParent, DateTimeOffset now, Guid? actor)
    {
        if (newParent is null)
        {
            if (!LocationHierarchy.CanBeRoot(Kind))
            {
                throw new InvalidOperationException($"A {Kind} cannot become a root location.");
            }
        }
        else
        {
            if (newParent.Id == Id || IsAncestorOf(newParent))
            {
                throw new InvalidOperationException("A location cannot be moved beneath itself.");
            }

            if (!newParent.CanAdopt(Kind))
            {
                throw new InvalidOperationException($"A {Kind} cannot sit under a {newParent.Kind}.");
            }
        }

        var oldPath = Path;
        var oldFullPath = FullPath;
        var oldDepth = Depth;

        ParentId = newParent?.Id;
        Path = ComposePath(newParent?.Path, Id);
        FullPath = ComposeFullPath(newParent?.FullPath, Name);
        Depth = newParent is null ? 0 : newParent.Depth + 1;
        Touch(now, actor);

        return new SubtreeRewrite(oldPath, Path, oldFullPath, FullPath, Depth - oldDepth);
    }

    /// <summary>
    /// The display path a child of <paramref name="parentFullPath"/> named
    /// <paramref name="name"/> has. Exposed so the seeder and the tests compose paths the
    /// same way the entity does.
    /// </summary>
    /// <param name="parentFullPath">The parent's display path, or <see langword="null"/> at the root.</param>
    /// <param name="name">The child's own name.</param>
    public static string ComposeFullPath(string? parentFullPath, string name) =>
        string.IsNullOrEmpty(parentFullPath) ? name : parentFullPath + PathSeparator + name;

    private static string ComposePath(string? parentPath, Guid id) =>
        // "N" rather than "D": the same information in 32 characters instead of 36,
        // which is 40 characters saved off a five-level path.
        (parentPath ?? "/") + id.ToString("N") + "/";

    private static string Normalize(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();

        return trimmed.Length <= NameMaxLength
            ? trimmed
            : throw new ArgumentException($"A location name may be at most {NameMaxLength} characters.", nameof(name));
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
            : throw new ArgumentException($"A location description may be at most {DescriptionMaxLength} characters.", nameof(description));
    }

    private void Touch(DateTimeOffset now, Guid? actor)
    {
        UpdatedAt = now;
        UpdatedBy = actor;
    }
}
