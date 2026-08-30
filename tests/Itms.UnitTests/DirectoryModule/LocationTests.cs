using Itms.Modules.Directory.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.DirectoryModule;

/// <summary>
/// The path arithmetic. Everything the module does efficiently — reading a full path in
/// one row, finding a subtree with one prefix match, refusing a cycle without a query —
/// rests on these two columns being composed correctly.
/// </summary>
public sealed class LocationTests
{
    private static readonly Guid Actor = Guid.CreateVersion7();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_root_starts_the_path_and_sits_at_depth_zero()
    {
        var root = Root("Northvale Utilities");

        root.ParentId.ShouldBeNull();
        root.Depth.ShouldBe(0);
        root.FullPath.ShouldBe("Northvale Utilities");
        root.Path.ShouldBe($"/{root.Id:N}/");
    }

    [Fact]
    public void A_child_extends_both_paths_and_increments_the_depth()
    {
        var root = Root("Northvale Utilities");
        var site = Child(root, "Head Office", LocationKind.Site);
        var room = Child(site, "Cabinet A", LocationKind.Room);

        room.ParentId.ShouldBe(site.Id);
        room.Depth.ShouldBe(2);
        room.FullPath.ShouldBe("Northvale Utilities / Head Office / Cabinet A");
        room.Path.ShouldBe($"/{root.Id:N}/{site.Id:N}/{room.Id:N}/");
    }

    /// <summary>
    /// The property the whole subtree query rests on: a descendant's path starts with
    /// its ancestor's, and nothing else's does.
    /// </summary>
    [Fact]
    public void A_descendant_path_starts_with_its_ancestor_path()
    {
        var root = Root("Northvale Utilities");
        var site = Child(root, "Head Office", LocationKind.Site);
        var room = Child(site, "Cabinet A", LocationKind.Room);
        var otherSite = Child(root, "Riverside", LocationKind.Site);

        room.Path.ShouldStartWith(site.Path);
        room.Path.ShouldStartWith(root.Path);
        otherSite.Path.ShouldNotStartWith(site.Path);

        site.IsAncestorOf(room).ShouldBeTrue();
        root.IsAncestorOf(room).ShouldBeTrue();
        site.IsAncestorOf(otherSite).ShouldBeFalse();
        room.IsAncestorOf(site).ShouldBeFalse();
        site.IsAncestorOf(site).ShouldBeFalse();
    }

    [Fact]
    public void Names_are_trimmed_and_normalised_for_the_uniqueness_constraint()
    {
        var root = Root("  Northvale Utilities  ");

        root.Name.ShouldBe("Northvale Utilities");
        root.NormalizedName.ShouldBe("NORTHVALE UTILITIES");
    }

    [Fact]
    public void A_root_that_is_not_an_organization_is_refused() =>
        Should.Throw<InvalidOperationException>(() =>
            Location.Create(parent: null, "Head Office", LocationKind.Site, null, _clock.UtcNow, Actor));

    [Fact]
    public void An_inverted_placement_is_refused_by_the_entity_even_if_a_handler_forgets_to_check()
    {
        var root = Root("Northvale Utilities");
        var room = Child(root, "Cabinet A", LocationKind.Room);

        Should.Throw<InvalidOperationException>(() =>
            Location.Create(room, "Admin Building", LocationKind.Building, null, _clock.UtcNow, Actor));
    }

    [Fact]
    public void A_blank_name_is_refused() =>
        Should.Throw<ArgumentException>(() =>
            Location.Create(parent: null, "   ", LocationKind.Organization, null, _clock.UtcNow, Actor));

    [Fact]
    public void An_over_long_name_is_refused() =>
        Should.Throw<ArgumentException>(() => Location.Create(
            parent: null,
            new string('x', Location.NameMaxLength + 1),
            LocationKind.Organization,
            null,
            _clock.UtcNow,
            Actor));

    [Fact]
    public void Renaming_changes_the_display_path_but_not_the_id_path()
    {
        var root = Root("Northvale Utilities");
        var site = Child(root, "Head Office", LocationKind.Site);
        var pathBefore = site.Path;

        _clock.Advance(TimeSpan.FromHours(1));
        var rewrite = site.Rename("Head Office North", root.FullPath, _clock.UtcNow, Actor);

        site.Name.ShouldBe("Head Office North");
        site.FullPath.ShouldBe("Northvale Utilities / Head Office North");
        site.Path.ShouldBe(pathBefore);
        site.UpdatedAt.ShouldBe(_clock.UtcNow);
        site.UpdatedBy.ShouldBe(Actor);

        // The id path is untouched, so which rows are descendants does not change —
        // only the text they display.
        rewrite.OldPath.ShouldBe(pathBefore);
        rewrite.NewPath.ShouldBe(pathBefore);
        rewrite.OldFullPath.ShouldBe("Northvale Utilities / Head Office");
        rewrite.NewFullPath.ShouldBe("Northvale Utilities / Head Office North");
        rewrite.DepthShift.ShouldBe(0);
        rewrite.IsNoop.ShouldBeFalse();
    }

    /// <summary>
    /// The arithmetic the subtree UPDATE performs, asserted here so the SQL in
    /// <c>LocationQueries</c> has something to be checked against.
    /// </summary>
    [Fact]
    public void A_rewrite_reattaches_a_descendant_path_by_prefix_length()
    {
        var root = Root("Northvale Utilities");
        var site = Child(root, "Head Office", LocationKind.Site);
        var room = Child(site, "Cabinet A", LocationKind.Room);

        var rewrite = site.Rename("Head Office North", root.FullPath, _clock.UtcNow, Actor);
        var rehung = rewrite.NewFullPath + room.FullPath[rewrite.OldFullPath.Length..];

        rehung.ShouldBe("Northvale Utilities / Head Office North / Cabinet A");
    }

    [Fact]
    public void Renaming_to_the_same_name_is_a_noop_rewrite()
    {
        var root = Root("Northvale Utilities");
        var rewrite = root.Rename("Northvale Utilities", parentFullPath: null, _clock.UtcNow, Actor);

        rewrite.IsNoop.ShouldBeTrue();
    }

    [Fact]
    public void Moving_recomputes_the_id_path_the_display_path_and_the_depth()
    {
        var root = Root("Northvale Utilities");
        var head = Child(root, "Head Office", LocationKind.Site);
        var riverside = Child(root, "Riverside", LocationKind.Site);
        var building = Child(head, "Admin Building", LocationKind.Building);

        var rewrite = building.MoveTo(riverside, _clock.UtcNow, Actor);

        building.ParentId.ShouldBe(riverside.Id);
        building.Depth.ShouldBe(2);
        building.FullPath.ShouldBe("Northvale Utilities / Riverside / Admin Building");
        building.Path.ShouldBe($"{riverside.Path}{building.Id:N}/");

        rewrite.OldFullPath.ShouldBe("Northvale Utilities / Head Office / Admin Building");
        rewrite.NewFullPath.ShouldBe("Northvale Utilities / Riverside / Admin Building");
        rewrite.DepthShift.ShouldBe(0);
    }

    [Fact]
    public void Moving_up_a_level_reports_a_negative_depth_shift()
    {
        var root = Root("Northvale Utilities");
        var site = Child(root, "Head Office", LocationKind.Site);
        var building = Child(site, "Admin Building", LocationKind.Building);
        var room = Child(building, "Cabinet A", LocationKind.Room);

        var rewrite = room.MoveTo(site, _clock.UtcNow, Actor);

        room.Depth.ShouldBe(2);
        rewrite.DepthShift.ShouldBe(-1);
    }

    [Fact]
    public void A_location_cannot_be_moved_beneath_itself()
    {
        var root = Root("Northvale Utilities");
        var site = Child(root, "Head Office", LocationKind.Site);

        Should.Throw<InvalidOperationException>(() => site.MoveTo(site, _clock.UtcNow, Actor));
    }

    [Fact]
    public void A_location_cannot_be_moved_beneath_its_own_descendant()
    {
        var root = Root("Northvale Utilities");
        var site = Child(root, "Head Office", LocationKind.Site);
        var building = Child(site, "Admin Building", LocationKind.Building);

        Should.Throw<InvalidOperationException>(() => site.MoveTo(building, _clock.UtcNow, Actor));
    }

    [Fact]
    public void A_move_that_would_invert_the_hierarchy_is_refused()
    {
        var root = Root("Northvale Utilities");
        var site = Child(root, "Head Office", LocationKind.Site);
        var room = Child(site, "Cabinet A", LocationKind.Room);
        var building = Child(site, "Admin Building", LocationKind.Building);

        Should.Throw<InvalidOperationException>(() => building.MoveTo(room, _clock.UtcNow, Actor));
    }

    [Fact]
    public void Only_an_organization_may_be_moved_to_the_root()
    {
        var root = Root("Northvale Utilities");
        var site = Child(root, "Head Office", LocationKind.Site);

        Should.Throw<InvalidOperationException>(() => site.MoveTo(newParent: null, _clock.UtcNow, Actor));
    }

    /// <summary>
    /// The deepest legal branch is Organization → Site → Building → Floor → Room, which
    /// puts a room at depth 4 and exactly at the limit. The room refuses to adopt on both
    /// grounds — rank and depth — and the depth one is the backstop being asserted here.
    /// </summary>
    [Fact]
    public void The_deepest_legal_branch_reaches_the_depth_limit_exactly()
    {
        var root = Root("Northvale Utilities");
        var site = Child(root, "Head Office", LocationKind.Site);
        var building = Child(site, "Admin Building", LocationKind.Building);
        var floor = Child(building, "Ground Floor", LocationKind.Floor);
        var room = Child(floor, "Server Room G-04", LocationKind.Room);

        room.Depth.ShouldBe(LocationHierarchy.MaxDepth - 1);
        floor.CanAdopt(LocationKind.Room).ShouldBeTrue();

        // Depth + 1 is no longer below the limit, so nothing may hang off a room even if
        // a future kind ranked below one.
        room.CanAdopt(LocationKind.Room).ShouldBeFalse();
    }

    [Fact]
    public void Describing_replaces_the_text_and_blank_clears_it()
    {
        var root = Root("Northvale Utilities");

        root.Describe("  The whole organisation.  ", _clock.UtcNow, Actor);
        root.Description.ShouldBe("The whole organisation.");

        root.Describe("   ", _clock.UtcNow, Actor);
        root.Description.ShouldBeNull();
    }

    private Location Root(string name) =>
        Location.Create(parent: null, name, LocationKind.Organization, null, _clock.UtcNow, Actor);

    private Location Child(Location parent, string name, LocationKind kind) =>
        Location.Create(parent, name, kind, null, _clock.UtcNow, Actor);
}
