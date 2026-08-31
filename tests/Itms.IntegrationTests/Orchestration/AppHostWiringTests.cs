using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging.Abstractions;

namespace Itms.IntegrationTests.Orchestration;

/// <summary>
/// Asserts the shape of the Aspire application model built by
/// <c>src/Itms.AppHost/AppHost.cs</c>. These guard the WP-0.2 promise that every
/// backing service is orchestrated and every connection string flows from Aspire
/// rather than from a file in the repository.
/// </summary>
public sealed class AppHostWiringTests(AppHostFixture fixture) : IClassFixture<AppHostFixture>
{
    // Environment variables are resolved in Publish mode so connection strings come
    // back as unresolved expressions ("{itms.connectionString}") instead of values
    // that would require the containers to be running.
    private static readonly DistributedApplicationExecutionContext Describe = new(DistributedApplicationOperation.Publish);

    private IResource Resource(string name) =>
        fixture.Builder.Resources.SingleOrDefault(r => r.Name == name)
        ?? throw new InvalidOperationException(
            $"No resource named '{name}'. Present: {string.Join(", ", fixture.Builder.Resources.Select(r => r.Name))}.");

    [Fact]
    public void Every_backing_service_is_orchestrated()
    {
        var names = fixture.Builder.Resources.Select(r => r.Name).ToArray();

        names.ShouldContain("postgres");
        names.ShouldContain("itms");
        names.ShouldContain("redis");
        names.ShouldContain("mailhog");
        names.ShouldContain("web-host");
        names.ShouldContain("web-client");
    }

    [Fact]
    public async Task The_react_client_is_told_where_the_api_is()
    {
        var environment = await Environment("web-client");

        // The client proxies /api to this address (vite.config.ts). Without the
        // reference it would fall back to the launchSettings port and silently talk
        // to whatever happened to be listening there.
        environment.Keys.ShouldContain(key => key.StartsWith("services__web-host__", StringComparison.Ordinal));
    }

    [Fact]
    public void The_react_client_waits_for_the_api()
    {
        var waits = Resource("web-client").Annotations
            .OfType<WaitAnnotation>()
            .Select(w => w.Resource.Name)
            .ToArray();

        waits.ShouldContain("web-host");
    }

    [Fact]
    public void The_database_belongs_to_the_postgres_server()
    {
        var database = Resource("itms").ShouldBeOfType<PostgresDatabaseResource>();

        database.Parent.Name.ShouldBe("postgres");
        // ARCHITECTURE.md §4: one database, one schema per module.
        database.DatabaseName.ShouldBe("itms");
    }

    [Theory]
    // ARCHITECTURE.md §10 pins PostgreSQL at 17+; the other tags are pinned so a
    // dev machine and a production compose file cannot drift apart.
    [InlineData("postgres", "17.6")]
    [InlineData("redis", "7.4")]
    [InlineData("mailhog", "v1.0.1")]
    public void Container_image_tags_are_pinned(string resourceName, string expectedTag)
    {
        var image = Resource(resourceName).Annotations
            .OfType<ContainerImageAnnotation>()
            .ShouldHaveSingleItem();

        image.Tag.ShouldBe(expectedTag);
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("redis")]
    public void Stateful_containers_keep_their_data_in_a_named_volume(string resourceName)
    {
        var resource = Resource(resourceName);

        resource.TryGetContainerMounts(out var mounts).ShouldBeTrue();
        mounts.ShouldContain(m => m.Type == ContainerMountType.Volume);

        // Without a persistent lifetime the volume survives but the container is
        // recreated on every `aspire run`, which is slow and loses tuning.
        resource.Annotations.OfType<ContainerLifetimeAnnotation>()
            .ShouldContain(a => a.Lifetime == ContainerLifetime.Persistent);
    }

    [Fact]
    public async Task The_web_host_receives_both_connection_strings_from_aspire()
    {
        var environment = await Environment("web-host");

        environment.ShouldContainKey("ConnectionStrings__itms");
        environment["ConnectionStrings__itms"].ShouldContain("itms");

        environment.ShouldContainKey("ConnectionStrings__redis");
        environment["ConnectionStrings__redis"].ShouldContain("redis");
    }

    [Fact]
    public async Task The_web_host_receives_the_mailhog_smtp_endpoint()
    {
        var environment = await Environment("web-host");

        // SMTP has no Aspire connection-string convention, so the endpoint is bound
        // to the configuration keys the Notifications module will read.
        environment.ShouldContainKey("Smtp__Host");
        environment.ShouldContainKey("Smtp__Port");
        environment["Smtp__Host"].ShouldContain("mailhog");
        environment["Smtp__Port"].ShouldContain("mailhog");
    }

    [Fact]
    public void The_web_host_waits_for_everything_it_depends_on()
    {
        var waits = Resource("web-host").Annotations
            .OfType<WaitAnnotation>()
            .Select(w => w.Resource.Name)
            .ToArray();

        waits.ShouldContain("itms");
        waits.ShouldContain("redis");
        waits.ShouldContain("mailhog");
    }

    [Theory]
    [InlineData("mailhog")]
    [InlineData("web-host")]
    public void Resources_without_a_built_in_probe_declare_an_http_health_check(string resourceName)
    {
        // Postgres and Redis get their health checks from their Aspire integrations;
        // a raw container and a project resource have to say so explicitly, or the
        // dashboard reports them healthy the moment the process exists.
        Resource(resourceName).Annotations
            .OfType<HealthCheckAnnotation>()
            .ShouldNotBeEmpty();
    }

    private async Task<IReadOnlyDictionary<string, string>> Environment(string resourceName)
    {
        var configuration = await ExecutionConfigurationBuilder.Create(Resource(resourceName))
            .WithEnvironmentVariablesConfig()
            .BuildAsync(Describe, NullLogger.Instance, TestContext.Current.CancellationToken);

        return configuration.EnvironmentVariables.ToDictionary(StringComparer.Ordinal);
    }
}
