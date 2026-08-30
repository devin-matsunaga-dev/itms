using System.Reflection;
using Itms.Modules.Audit.Domain;
using Itms.Modules.Audit.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itms.ArchitectureTests;

/// <summary>
/// ARCHITECTURE.md invariant 10, enforced: audit entries are never modified or deleted
/// through any code path in this system.
/// </summary>
/// <remarks>
/// <para>
/// "Any code path in this system" is checkable because the reachable surface is small
/// and bounded by the module rules already asserted in <see cref="ModuleBoundaryTests"/>:
/// the audit table is owned by <c>Itms.Modules.Audit</c>, no module may reference another
/// module, and the only other project that can see it is the composition root. So the two
/// source trees scanned below are, provably, the only two that could contain such a path.
/// <see cref="Only_the_composition_root_may_reference_the_audit_module"/> is what keeps
/// that argument true.
/// </para>
/// <para>
/// The database enforces the same rule with a trigger, asserted separately by the
/// integration suite against a real <c>UPDATE</c> and a real <c>DELETE</c>. Neither guard
/// makes the other redundant: this one catches the code before it ships, that one catches
/// a hand-written statement that never goes through this code at all.
/// </para>
/// </remarks>
public sealed class AuditAppendOnlyTests
{
    // Every way EF Core offers to change or remove a row. A path into the audit table
    // has to go through one of them, and none of them may appear where one could.
    private static readonly string[] MutatingCalls =
    [
        "ExecuteDelete",
        "ExecuteDeleteAsync",
        "ExecuteUpdate",
        "ExecuteUpdateAsync",
        ".Remove(",
        ".RemoveRange(",
        ".Update(",
        ".UpdateRange(",
    ];

    /// <summary>
    /// The context hands out no <c>DbSet</c>. A <c>DbSet&lt;AuditRecord&gt;</c> property
    /// would put <c>Remove</c> and <c>ExecuteDelete</c> within reach of every caller, and
    /// no amount of discipline elsewhere would take them back.
    /// </summary>
    [Fact]
    public void The_audit_context_exposes_no_DbSet()
    {
        var setProperties = typeof(AuditDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType
                && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));

        setProperties.ShouldBeEmpty();
    }

    /// <summary>The context's only write is an append.</summary>
    [Fact]
    public void The_audit_context_offers_no_write_but_an_append()
    {
        var declared = typeof(AuditDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToArray();

        declared.ShouldBe([nameof(AuditDbContext.Query), nameof(AuditDbContext.AppendAsync)], ignoreOrder: true);
    }

    /// <summary>An audit row cannot be changed once it exists.</summary>
    [Fact]
    public void The_audit_record_exposes_no_public_setter() =>
        typeof(AuditRecord)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ShouldAllBe(p => p.SetMethod == null || !p.SetMethod.IsPublic);

    /// <summary>
    /// No source file that can reach the audit table changes or removes a row in it.
    /// </summary>
    [Theory]
    [InlineData("Modules/Itms.Modules.Audit")]
    [InlineData("Itms.Web.Host")]
    public void No_project_that_can_see_the_audit_table_updates_or_deletes_a_row(string projectPath)
    {
        var root = Path.Combine(RepositoryRoot(), "src", projectPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.Exists(root).ShouldBeTrue($"Expected to scan {root}.");

        var offenders = SourceFilesUnder(root)
            .Select(file => (File: file, Code: CodeOnly(File.ReadAllLines(file))))
            .Where(f => MutatingCalls.Any(call => f.Code.Contains(call, StringComparison.Ordinal)))
            .Select(f => Path.GetRelativePath(root, f.File))
            .ToArray();

        offenders.ShouldBeEmpty(
            $"An audit row is never updated or deleted (ARCHITECTURE.md invariant 10). Found: {string.Join(", ", offenders)}");
    }

    /// <summary>
    /// The audit module is referenced by the composition root and by nothing else. This
    /// is what bounds the scan above to two projects — a third project holding a
    /// reference would be a third place a mutation could hide.
    /// </summary>
    [Fact]
    public void Only_the_composition_root_may_reference_the_audit_module()
    {
        var audit = SolutionLayout.NameOf(typeof(Itms.Modules.Audit.AssemblyMarker).Assembly);

        var referencing = SolutionLayout.DeclaredReferences
            .Where(entry => entry.Value.Contains(audit, StringComparer.Ordinal))
            .Select(entry => entry.Key)
            .ToArray();

        referencing.ShouldBe(["Itms.Web.Host"]);
    }

    /// <summary>
    /// The file with its comment lines dropped. The rule is about what the code does, and
    /// the doc comment on <c>AuditDbContext</c> has to be able to name the very calls it
    /// explains the absence of.
    /// </summary>
    /// <param name="lines">The file's lines.</param>
    /// <returns>The remaining lines, joined.</returns>
    private static string CodeOnly(IEnumerable<string> lines) =>
        string.Join('\n', lines.Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    private static IEnumerable<string> SourceFilesUnder(string root) =>
        Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file =>
            {
                var relative = Path.GetRelativePath(root, file);
                // Build output is not source, and a migration's Down drops the table
                // wholesale — which is schema teardown, not a path that edits a row.
                return !relative.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && !relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && !relative.Contains(Path.DirectorySeparatorChar + "Migrations" + Path.DirectorySeparatorChar, StringComparison.Ordinal);
            });

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ITMS.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate ITMS.sln above the test output directory.");
    }
}
