namespace Itms.UnitTests;

/// <summary>
/// Proves the unit-test project is wired and discoverable. Domain tests replace
/// this from WP-0.3 onward.
/// </summary>
public sealed class SolutionSkeletonTests
{
    [Fact]
    public void Platform_assembly_is_loadable()
    {
        typeof(Itms.Platform.AssemblyMarker).Assembly.GetName().Name.ShouldBe("Itms.Platform");
    }
}
