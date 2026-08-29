using System.Reflection;
using System.Xml.Linq;

namespace Itms.ArchitectureTests;

/// <summary>
/// The assemblies and the project-reference graph the ARCHITECTURE.md §3 rules are
/// written against.
/// </summary>
/// <remarks>
/// <para>
/// The rules are checked two ways, because neither is sufficient alone. Reading the
/// <c>.csproj</c> files catches a reference that is <em>declared</em> but not yet used —
/// which is every module today, since they are still empty and the C# compiler omits
/// references it sees no types from. Reading the compiled assemblies catches a
/// reference that arrives transitively rather than through a project file.
/// </para>
/// <para>
/// Both views cover the source projects only. The test assemblies are excluded on
/// purpose: <c>Itms.ArchitectureTests</c> references every module — it cannot inspect
/// assemblies it does not reference — so a rule phrased as "nothing references two
/// modules" would fail on the very project enforcing it.
/// </para>
/// </remarks>
internal static class SolutionLayout
{
    /// <summary>The shared kernel assembly.</summary>
    public static Assembly Platform { get; } = typeof(Itms.Platform.AssemblyMarker).Assembly;

    /// <summary>The public contracts and domain events assembly.</summary>
    public static Assembly Contracts { get; } = typeof(Itms.Contracts.AssemblyMarker).Assembly;

    /// <summary>The in-process bus and its transactional outbox. Infrastructure, not a module.</summary>
    public static Assembly Messaging { get; } = typeof(Itms.Messaging.AssemblyMarker).Assembly;

    /// <summary>
    /// Every module assembly. Located through its <c>AssemblyMarker</c> type rather than
    /// by name, so dropping or renaming a project breaks the compile here instead of
    /// silently shrinking what the rules cover.
    /// </summary>
    public static IReadOnlyList<Assembly> Modules { get; } =
    [
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

    /// <summary>Every source assembly the rules cover: the shared kernel, contracts, the bus, and all modules.</summary>
    public static IReadOnlyList<Assembly> All { get; } = [Platform, Contracts, Messaging, .. Modules];

    /// <summary>The names of the module assemblies, for reference checks.</summary>
    public static IReadOnlySet<string> ModuleNames { get; } =
        Modules.Select(NameOf).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Every project under <c>src/</c>, mapped to the project names it declares a
    /// <c>ProjectReference</c> to. Declared references, not compiled ones.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> DeclaredReferences { get; } = ReadProjectGraph();

    /// <summary>The simple name of an assembly.</summary>
    public static string NameOf(Assembly assembly) => assembly.GetName().Name!;

    /// <summary>The assemblies <paramref name="assembly"/> actually references after compilation.</summary>
    public static IReadOnlyList<string> ReferencedAssemblies(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(a => a.Name!)];

    private static Dictionary<string, IReadOnlyList<string>> ReadProjectGraph()
    {
        var src = Path.Combine(FindRepositoryRoot(), "src");
        var graph = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var project in Directory.EnumerateFiles(src, "*.csproj", SearchOption.AllDirectories))
        {
            var references = XDocument.Load(project)
                .Descendants("ProjectReference")
                .Select(e => (string?)e.Attribute("Include"))
                .Where(include => !string.IsNullOrWhiteSpace(include))
                .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', Path.DirectorySeparatorChar)))
                .ToArray();

            graph[Path.GetFileNameWithoutExtension(project)] = references;
        }

        return graph;
    }

    private static string FindRepositoryRoot()
    {
        // The test binary sits under tests/<project>/bin/<config>/<tfm>; walk up to the
        // solution file rather than hard-coding how deep that is.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ITMS.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate ITMS.sln above the test output directory.");
    }
}
