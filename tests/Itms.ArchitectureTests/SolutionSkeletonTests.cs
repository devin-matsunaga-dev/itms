using System.Reflection;

namespace Itms.ArchitectureTests;

/// <summary>
/// Proves the architecture-test project is wired and can see every assembly it
/// will need to inspect. The rules from ARCHITECTURE.md §3 — Platform references
/// no module, modules do not reference each other, cross-module reads go through
/// Itms.Contracts — are asserted here in WP-0.3, where they become a build
/// failure in CI.
/// </summary>
public sealed class SolutionSkeletonTests
{
    /// <summary>
    /// The assemblies the §3 rules are written against. Referencing the marker
    /// types rather than loading by name means a renamed or dropped project
    /// breaks the compile instead of failing at runtime.
    /// </summary>
    private static readonly Assembly[] SolutionAssemblies =
    [
        typeof(Itms.Platform.AssemblyMarker).Assembly,
        typeof(Itms.Contracts.AssemblyMarker).Assembly,
        typeof(Itms.Modules.Identity.AssemblyMarker).Assembly,
        typeof(Itms.Modules.Directory.AssemblyMarker).Assembly,
        typeof(Itms.Modules.Helpdesk.AssemblyMarker).Assembly,
        typeof(Itms.Modules.Assets.AssemblyMarker).Assembly,
        typeof(Itms.Modules.Monitoring.AssemblyMarker).Assembly,
        typeof(Itms.Modules.Alerts.AssemblyMarker).Assembly,
        typeof(Itms.Modules.Knowledge.AssemblyMarker).Assembly,
        typeof(Itms.Modules.Search.AssemblyMarker).Assembly,
        typeof(Itms.Modules.Notifications.AssemblyMarker).Assembly,
        typeof(Itms.Modules.Reporting.AssemblyMarker).Assembly,
        typeof(Itms.Modules.Audit.AssemblyMarker).Assembly,
    ];

    [Fact]
    public void Every_assembly_the_architecture_rules_cover_is_present()
    {
        SolutionAssemblies.Length.ShouldBe(13);
        SolutionAssemblies.ShouldAllBe(a => a.GetName().Name!.StartsWith("Itms."));
    }
}
