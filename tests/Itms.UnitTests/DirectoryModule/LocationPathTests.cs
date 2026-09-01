using Itms.Modules.Directory.Domain;

namespace Itms.UnitTests.DirectoryModule;

/// <summary>
/// The path arithmetic read back against the path arithmetic that writes it. The
/// ancestor chain a cascading picker asks for is derived entirely from the materialised
/// id path, so if these two ever disagree the picker preselects the wrong rooms and
/// nothing else in the system notices.
/// </summary>
public sealed class LocationPathTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_root_path_holds_exactly_the_roots_own_id()
    {
        var root = Location.Create(null, "Northvale Utilities", LocationKind.Organization, null, Now, null);

        LocationPath.ParseIds(root.Path).ShouldBe([root.Id]);
    }

    /// <summary>
    /// The chain is root first and includes the node itself — which is what lets a picker
    /// opened on a room fill five selects from one response rather than walking upwards.
    /// </summary>
    [Fact]
    public void A_deep_path_reads_back_as_its_ancestors_root_first_including_the_node()
    {
        var tree = BuildFiveLevels();

        LocationPath.ParseIds(tree.Room.Path)
            .ShouldBe([tree.Root.Id, tree.Site.Id, tree.Building.Id, tree.Floor.Id, tree.Room.Id]);
    }

    /// <summary>
    /// Depth and the path length are two statements of the same fact, and the ancestor
    /// query orders by depth while the path is what produced the ids. They have to agree.
    /// </summary>
    [Fact]
    public void The_number_of_ids_in_a_path_is_the_nodes_depth_plus_one()
    {
        var tree = BuildFiveLevels();

        foreach (var node in new[] { tree.Root, tree.Site, tree.Building, tree.Floor, tree.Room })
        {
            LocationPath.ParseIds(node.Path).Count.ShouldBe(node.Depth + 1);
        }
    }

    /// <summary>A move rewrites the id path, so the chain has to follow it.</summary>
    [Fact]
    public void Moving_a_node_changes_the_chain_it_reads_back_as()
    {
        var tree = BuildFiveLevels();
        var riverside = Location.Create(tree.Root, "Riverside Plant", LocationKind.Site, null, Now, null);

        tree.Building.MoveTo(riverside, Now, null);

        LocationPath.ParseIds(tree.Building.Path)
            .ShouldBe([tree.Root.Id, riverside.Id, tree.Building.Id]);
    }

    /// <summary>A rename touches no id, so the chain is untouched by one.</summary>
    [Fact]
    public void Renaming_a_node_leaves_its_chain_alone()
    {
        var tree = BuildFiveLevels();
        var before = LocationPath.ParseIds(tree.Site.Path);

        tree.Site.Rename("Head Office North", tree.Root.FullPath, Now, null);

        LocationPath.ParseIds(tree.Site.Path).ShouldBe(before);
    }

    [Fact]
    public void An_empty_path_holds_no_ids() =>
        LocationPath.ParseIds("/").ShouldBeEmpty();

    /// <summary>
    /// The column is written only by the entity and rewritten only by a prefix update, so
    /// a malformed segment means the row was edited by hand. Throwing beats returning a
    /// silently short chain, which a picker would render as a shallower tree.
    /// </summary>
    [Theory]
    [InlineData("/not-a-guid/")]
    [InlineData("/0123456789abcdef/")]
    public void A_hand_edited_path_is_rejected_rather_than_read_as_a_shorter_chain(string path) =>
        Should.Throw<FormatException>(() => LocationPath.ParseIds(path));

    private static (Location Root, Location Site, Location Building, Location Floor, Location Room) BuildFiveLevels()
    {
        var root = Location.Create(null, "Northvale Utilities", LocationKind.Organization, null, Now, null);
        var site = Location.Create(root, "Head Office", LocationKind.Site, null, Now, null);
        var building = Location.Create(site, "Admin Building", LocationKind.Building, null, Now, null);
        var floor = Location.Create(building, "Ground Floor", LocationKind.Floor, null, Now, null);
        var room = Location.Create(floor, "Server Room G-04", LocationKind.Room, null, Now, null);

        return (root, site, building, floor, room);
    }
}
