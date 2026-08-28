using System.Text.Json;

namespace Itms.IntegrationTests.Orchestration;

/// <summary>
/// WP-0.2 requires that connection strings flow from Aspire and are hardcoded
/// nowhere. CONVENTIONS.md goes further: nothing sensitive belongs in an
/// appsettings file at all. This walks the checked-in configuration and fails if
/// one grows a connection string or a credential.
/// </summary>
public sealed class ConfigurationHygieneTests
{
    private static readonly string[] ForbiddenKeys = ["ConnectionStrings", "Password", "ApiKey", "Secret"];

    [Fact]
    public void No_appsettings_file_declares_a_connection_string_or_a_credential()
    {
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(RepositoryRoot(), "appsettings*.json", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(file));

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (ForbiddenKeys.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                {
                    offenders.Add($"{Path.GetRelativePath(RepositoryRoot(), file)} → {property.Name}");
                }
            }
        }

        offenders.ShouldBeEmpty(
            "Connection strings and credentials come from Aspire, environment, or user-secrets — never from a file in the repository.");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ITMS.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root from " + AppContext.BaseDirectory);
    }
}
