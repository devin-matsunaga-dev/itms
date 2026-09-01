using Itms.Modules.Directory.Domain;

namespace Itms.Modules.Directory.Features.Locations;

/// <summary>
/// Narrows a location query to the nodes that could legally be a parent of a given kind.
/// </summary>
/// <remarks>
/// This is what makes a cascading picker offer only the levels a placement is actually
/// allowed at, without the client re-implementing <see cref="LocationHierarchy"/> in
/// TypeScript — the second copy of a rule is where the two start disagreeing, and here
/// the disagreement would be a 409 the user could not have predicted from the form.
/// </remarks>
internal static class LocationAdoption
{
    /// <summary>
    /// Restricts <paramref name="query"/> to nodes that could adopt a
    /// <paramref name="childKind"/>.
    /// </summary>
    /// <param name="query">The location query to narrow.</param>
    /// <param name="childKind">The kind being placed.</param>
    /// <returns>The narrowed query.</returns>
    /// <remarks>
    /// Two conditions, matching <see cref="Location.CanAdopt"/> exactly: the parent's kind
    /// must rank above the child's, and there must be a level left before
    /// <see cref="LocationHierarchy.MaxDepth"/>. The rank half is resolved to a set of
    /// kinds in memory because rank is a function rather than a column; the depth half is
    /// arithmetic PostgreSQL can do itself.
    /// </remarks>
    public static IQueryable<Location> Filter(IQueryable<Location> query, LocationKind childKind)
    {
        ArgumentNullException.ThrowIfNull(query);

        var parentKinds = LocationHierarchy.KindsThatCanContain(childKind);

        return query.Where(candidate =>
            parentKinds.Contains(candidate.Kind) &&
            candidate.Depth + 1 < LocationHierarchy.MaxDepth);
    }
}
