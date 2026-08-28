using Aspire.Hosting.Testing;

namespace Itms.IntegrationTests.Orchestration;

/// <summary>
/// Builds the Aspire application model once for the whole class. The model is
/// only described here, never started, so these tests need no Docker daemon and
/// stay inside the two-minute budget CONVENTIONS.md sets for the suite.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    private IDistributedApplicationTestingBuilder? _builder;

    public IDistributedApplicationTestingBuilder Builder =>
        _builder ?? throw new InvalidOperationException("The application model has not been created yet.");

    public async ValueTask InitializeAsync()
    {
        _builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Itms_AppHost>();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
