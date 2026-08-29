namespace Itms.ArchitectureTests;

/// <summary>
/// ARCHITECTURE.md §3, enforced. These rules are the reason the monolith stays
/// modular: they fail the build the first time a module reaches into another one,
/// which is years before the coupling would otherwise be noticed.
/// </summary>
public sealed class ModuleBoundaryTests
{
    /// <summary>Rule 4: <c>Platform</c> holds shared primitives and never references a module.</summary>
    [Fact]
    public void Platform_references_no_module()
    {
        SolutionLayout.DeclaredReferences[SolutionLayout.NameOf(SolutionLayout.Platform)]
            .ShouldNotContain(reference => SolutionLayout.ModuleNames.Contains(reference));

        SolutionLayout.ReferencedAssemblies(SolutionLayout.Platform)
            .ShouldNotContain(reference => SolutionLayout.ModuleNames.Contains(reference));
    }

    /// <summary>
    /// The shared kernel is also the wrong place for contracts: a module that needed
    /// <c>Platform</c> in order to see <c>IAssetLookup</c> would drag the kernel into
    /// every cross-module read.
    /// </summary>
    [Fact]
    public void Platform_references_no_contracts()
    {
        SolutionLayout.DeclaredReferences[SolutionLayout.NameOf(SolutionLayout.Platform)]
            .ShouldNotContain(SolutionLayout.NameOf(SolutionLayout.Contracts));
    }

    /// <summary>
    /// <c>Contracts</c> is the interface surface every module can see, so it must depend
    /// on nothing in the solution — a reference in either direction would make it a
    /// second shared kernel.
    /// </summary>
    [Fact]
    public void Contracts_references_nothing_in_the_solution()
    {
        SolutionLayout.DeclaredReferences[SolutionLayout.NameOf(SolutionLayout.Contracts)].ShouldBeEmpty();
    }

    /// <summary>
    /// Rules 1 and 2: a module never references another module. Cross-module reads go
    /// through the owning module's contract interface.
    /// </summary>
    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_references_no_other_module(string moduleName)
    {
        var others = SolutionLayout.ModuleNames.Where(name => name != moduleName).ToArray();

        SolutionLayout.DeclaredReferences[moduleName].ShouldNotContain(reference => others.Contains(reference, StringComparer.Ordinal));

        var module = SolutionLayout.Modules.Single(m => SolutionLayout.NameOf(m) == moduleName);
        SolutionLayout.ReferencedAssemblies(module).ShouldNotContain(reference => others.Contains(reference, StringComparer.Ordinal));
    }

    /// <summary>
    /// A module's only permitted in-solution dependencies are the shared kernel and the
    /// contracts assembly. Anything else is either a boundary violation or a project
    /// that has appeared without a rule covering it.
    /// </summary>
    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_depends_only_on_platform_and_contracts(string moduleName)
    {
        string[] allowed =
        [
            SolutionLayout.NameOf(SolutionLayout.Platform),
            SolutionLayout.NameOf(SolutionLayout.Contracts),
        ];

        SolutionLayout.DeclaredReferences[moduleName]
            .ShouldAllBe(reference => allowed.Contains(reference, StringComparer.Ordinal));
    }

    /// <summary>
    /// The rules cover source projects only. <c>Itms.ArchitectureTests</c> references
    /// every module by design — it cannot inspect what it does not reference — so this
    /// guards against a future rule being phrased in a way that would catch it
    /// (recorded in STATUS.md at WP-0.2).
    /// </summary>
    [Fact]
    public void Rules_are_written_against_source_assemblies_only()
    {
        SolutionLayout.All.ShouldAllBe(a => !SolutionLayout.NameOf(a).EndsWith("Tests", StringComparison.Ordinal));
        SolutionLayout.DeclaredReferences.Keys.ShouldAllBe(name => !name.EndsWith("Tests", StringComparison.Ordinal));
    }

    /// <summary>Every module project is present in the graph the rules read.</summary>
    [Fact]
    public void Every_module_project_is_covered_by_the_rules()
    {
        SolutionLayout.Modules.Count.ShouldBe(11);
        SolutionLayout.ModuleNames.ShouldAllBe(name => SolutionLayout.DeclaredReferences.ContainsKey(name));
    }

    public static TheoryData<string> ModuleNames() => [.. SolutionLayout.ModuleNames];
}
