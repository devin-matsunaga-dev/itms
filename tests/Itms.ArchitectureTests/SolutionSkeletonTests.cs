namespace Itms.ArchitectureTests;

/// <summary>
/// Proves the architecture-test project can still see every assembly the §3 rules are
/// written against. The rules themselves live in <see cref="ModuleBoundaryTests"/> and
/// <see cref="ContractShapeTests"/>; this is the guard that they are not quietly
/// inspecting a shrinking set.
/// </summary>
public sealed class SolutionSkeletonTests
{
    [Fact]
    public void Every_assembly_the_architecture_rules_cover_is_present()
    {
        // Platform, Contracts, Messaging, and the eleven modules.
        SolutionLayout.All.Count.ShouldBe(14);
        SolutionLayout.All.ShouldAllBe(a => a.GetName().Name!.StartsWith("Itms.", StringComparison.Ordinal));
    }
}
