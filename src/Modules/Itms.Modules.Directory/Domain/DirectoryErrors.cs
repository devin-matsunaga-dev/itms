using Itms.Platform.Results;

namespace Itms.Modules.Directory.Domain;

/// <summary>
/// Every failure this module can return, written once.
/// </summary>
/// <remarks>
/// The codes are part of the API surface — clients switch on them — so they live in one
/// file where a reword is visible in review rather than being spelled out at each of the
/// dozen call sites that can produce them.
/// </remarks>
internal static class DirectoryErrors
{
    public static Error DepartmentNotFound() =>
        Error.NotFound("directory.department_not_found", "No such department.");

    public static Error DuplicateDepartmentName(string name) =>
        Error.Conflict("directory.duplicate_department_name", $"A department named '{name}' already exists.");

    public static Error DuplicateDepartmentCode(string code) =>
        Error.Conflict("directory.duplicate_department_code", $"A department with the code '{code}' already exists.");

    public static Error LocationNotFound() =>
        Error.NotFound("directory.location_not_found", "No such location.");

    public static Error ParentNotFound() =>
        Error.NotFound("directory.parent_not_found", "No such parent location.");

    public static Error DuplicateLocationName(string name) =>
        Error.Conflict(
            "directory.duplicate_location_name",
            $"A location named '{name}' already exists under the same parent.");

    public static Error RootMustBeOrganization(LocationKind kind) =>
        Error.Conflict(
            "directory.illegal_root_kind",
            $"A location with no parent must be an {LocationHierarchy.RootKind}, and this one is a {kind}.");

    public static Error IllegalPlacement(LocationKind parentKind, LocationKind childKind) =>
        Error.Conflict(
            "directory.illegal_placement",
            $"A {childKind} cannot sit under a {parentKind}. The hierarchy runs Organization, Site, Building, Floor or Area, Room.");

    public static Error TooDeep() =>
        Error.Conflict(
            "directory.location_too_deep",
            $"The location tree is limited to {LocationHierarchy.MaxDepth} levels.");

    public static Error WouldCreateCycle() =>
        Error.Conflict(
            "directory.location_cycle",
            "A location cannot be moved beneath itself or one of its own descendants.");

    /// <summary>
    /// The refusal WP-0.6 names by name: deleting a location that still has children.
    /// The count is included because "it has children" without saying how many sends the
    /// operator hunting through the tree.
    /// </summary>
    public static Error LocationHasChildren(string name, int childCount) =>
        Error.Conflict(
            "directory.location_has_children",
            $"'{name}' still contains {childCount} location{(childCount == 1 ? string.Empty : "s")}. Delete or move them first.");

    /// <summary>
    /// The second refusal a delete can hit: the node is a leaf, but rows in other modules
    /// still point at it.
    /// </summary>
    /// <remarks>
    /// A distinct code from <see cref="LocationHasChildren"/> because the two need
    /// different actions — one is "empty the subtree", the other is "move the equipment
    /// and the people" — and a client that showed one message for both would be telling
    /// an administrator to go and look in the wrong place. The breakdown is in the message
    /// because a bare "it is in use" sends them hunting; <c>GET /locations/{id}/usage</c>
    /// is the same figures ahead of the click.
    /// </remarks>
    /// <param name="name">The location's own name.</param>
    /// <param name="breakdown">The per-module counts, already rendered — "3 assets and 1 user".</param>
    public static Error LocationInUse(string name, string breakdown) =>
        Error.Conflict(
            "directory.location_in_use",
            $"'{name}' is still referenced by {breakdown}. Move or reassign them first.");
}
