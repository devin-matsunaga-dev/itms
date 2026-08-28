namespace Itms.IntegrationTests;

/// <summary>
/// Proves the integration-test project is wired and discoverable. Real endpoint
/// and persistence tests, with the shared Testcontainers PostgreSQL fixture
/// required by CONVENTIONS.md, arrive with WP-0.4.
/// </summary>
public sealed class SolutionSkeletonTests
{
    [Fact]
    public void Contracts_assembly_is_loadable()
    {
        typeof(Itms.Contracts.AssemblyMarker).Assembly.GetName().Name.ShouldBe("Itms.Contracts");
    }
}
