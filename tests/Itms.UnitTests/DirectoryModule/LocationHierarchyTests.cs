using Itms.Modules.Directory.Domain;

namespace Itms.UnitTests.DirectoryModule;

/// <summary>
/// The ordering rule of the location tree. It is the one thing in this module that a
/// later package could quietly widen, so every legal and illegal pairing is named.
/// </summary>
public sealed class LocationHierarchyTests
{
    [Theory]
    [InlineData(LocationKind.Organization, 0)]
    [InlineData(LocationKind.Site, 1)]
    [InlineData(LocationKind.Building, 2)]
    [InlineData(LocationKind.Floor, 3)]
    [InlineData(LocationKind.Area, 3)]
    [InlineData(LocationKind.Room, 4)]
    public void Rank_follows_the_five_levels_the_spec_names(LocationKind kind, int expected) =>
        LocationHierarchy.RankOf(kind).ShouldBe(expected);

    /// <summary>SPEC.md §5 writes the level as "Floor/Area" — one level, two labels.</summary>
    [Fact]
    public void Floor_and_area_share_a_level_and_cannot_contain_each_other()
    {
        LocationHierarchy.RankOf(LocationKind.Floor).ShouldBe(LocationHierarchy.RankOf(LocationKind.Area));
        LocationHierarchy.CanContain(LocationKind.Floor, LocationKind.Area).ShouldBeFalse();
        LocationHierarchy.CanContain(LocationKind.Area, LocationKind.Floor).ShouldBeFalse();
    }

    [Theory]
    [InlineData(LocationKind.Organization, LocationKind.Site)]
    [InlineData(LocationKind.Site, LocationKind.Building)]
    [InlineData(LocationKind.Building, LocationKind.Floor)]
    [InlineData(LocationKind.Building, LocationKind.Area)]
    [InlineData(LocationKind.Floor, LocationKind.Room)]
    [InlineData(LocationKind.Area, LocationKind.Room)]
    public void A_lower_rank_may_sit_under_a_higher_one(LocationKind parent, LocationKind child) =>
        LocationHierarchy.CanContain(parent, child).ShouldBeTrue();

    /// <summary>
    /// A pump station is a site with a cabinet in it and no building worth inventing, so
    /// the rule skips levels rather than requiring every one of them.
    /// </summary>
    [Theory]
    [InlineData(LocationKind.Site, LocationKind.Room)]
    [InlineData(LocationKind.Organization, LocationKind.Room)]
    [InlineData(LocationKind.Site, LocationKind.Area)]
    public void Levels_may_be_skipped(LocationKind parent, LocationKind child) =>
        LocationHierarchy.CanContain(parent, child).ShouldBeTrue();

    [Theory]
    [InlineData(LocationKind.Room, LocationKind.Building)]
    [InlineData(LocationKind.Building, LocationKind.Site)]
    [InlineData(LocationKind.Site, LocationKind.Organization)]
    [InlineData(LocationKind.Room, LocationKind.Room)]
    [InlineData(LocationKind.Site, LocationKind.Site)]
    public void The_hierarchy_cannot_be_inverted_or_flattened(LocationKind parent, LocationKind child) =>
        LocationHierarchy.CanContain(parent, child).ShouldBeFalse();

    [Fact]
    public void Only_an_organization_may_be_a_root()
    {
        LocationHierarchy.CanBeRoot(LocationKind.Organization).ShouldBeTrue();

        foreach (var kind in Enum.GetValues<LocationKind>().Where(k => k != LocationKind.Organization))
        {
            LocationHierarchy.CanBeRoot(kind).ShouldBeFalse();
        }
    }

    [Fact]
    public void An_undeclared_kind_is_rejected_rather_than_silently_ranked() =>
        Should.Throw<ArgumentOutOfRangeException>(() => LocationHierarchy.RankOf((LocationKind)99));
}
