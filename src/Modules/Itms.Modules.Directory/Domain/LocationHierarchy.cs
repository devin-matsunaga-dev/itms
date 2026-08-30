namespace Itms.Modules.Directory.Domain;

/// <summary>
/// The ordering rule of the location tree, as pure functions so it can be asserted
/// without a database.
/// </summary>
/// <remarks>
/// The rule is <em>descending rank</em> rather than exact adjacency. SPEC.md §5 says
/// the tree supports "offices, plants, remote facilities, pump stations", and a pump
/// station is a site with a room in it and no building or floor worth inventing.
/// Requiring every level to be present would force operators to create placeholder
/// nodes, which is how a directory stops matching the building it describes. What is
/// forbidden is inversion: a Room never contains a Building.
/// </remarks>
public static class LocationHierarchy
{
    /// <summary>
    /// How many levels the hierarchy has, and therefore the exclusive upper bound on a
    /// node's <c>Depth</c>: the root is 0 and a room is 4.
    /// </summary>
    /// <remarks>
    /// The rank rule already guarantees this — depth rises by one per level while rank
    /// rises by at least one, so depth never exceeds rank, and rank stops at 4. The
    /// constant and the checks written against it are the backstop that keeps the
    /// materialised <c>path</c> column bounded if a sixth kind is ever added, at which
    /// point the guard stops being unreachable and starts being the thing that holds.
    /// </remarks>
    public const int MaxDepth = 5;

    /// <summary>The kind a root node must be. The tree starts at the organisation.</summary>
    public const LocationKind RootKind = LocationKind.Organization;

    /// <summary>
    /// Where <paramref name="kind"/> sits in the hierarchy, lowest first.
    /// <see cref="LocationKind.Floor"/> and <see cref="LocationKind.Area"/> share a rank.
    /// </summary>
    /// <param name="kind">The kind to rank.</param>
    /// <returns>The 0-based rank.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
    public static int RankOf(LocationKind kind) => kind switch
    {
        LocationKind.Organization => 0,
        LocationKind.Site => 1,
        LocationKind.Building => 2,
        LocationKind.Floor or LocationKind.Area => 3,
        LocationKind.Room => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a location kind."),
    };

    /// <summary>
    /// Whether a node of <paramref name="childKind"/> may sit under one of
    /// <paramref name="parentKind"/>.
    /// </summary>
    /// <param name="parentKind">The proposed parent's kind.</param>
    /// <param name="childKind">The child's kind.</param>
    /// <returns>True when the child ranks strictly below the parent.</returns>
    public static bool CanContain(LocationKind parentKind, LocationKind childKind) =>
        RankOf(childKind) > RankOf(parentKind);

    /// <summary>Whether <paramref name="kind"/> may sit at the root of the tree.</summary>
    /// <param name="kind">The kind to check.</param>
    public static bool CanBeRoot(LocationKind kind) => kind == RootKind;
}
