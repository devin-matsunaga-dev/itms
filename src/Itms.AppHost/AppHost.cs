// Aspire orchestration for ITMS (ARCHITECTURE.md §2 and §10).
//
// Every connection string and endpoint the web host consumes is produced here and
// injected as configuration. Nothing that addresses a backing service is allowed
// to appear in an appsettings file — an integration test asserts that.
//
// Container image tags are pinned rather than floated so a dev machine and a
// production compose file agree on the version they are running.

using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL is the only durable store (ARCHITECTURE.md §4): one database, one
// schema per module. The generated password lives in user-secrets, never in the
// repository. A data volume plus a persistent container lifetime means the
// database survives `aspire run` cycles, which is what makes migrations and seed
// data usable across sessions.
var postgres = builder.AddPostgres("postgres")
    .WithImageTag("17.6")
    .WithDataVolume("itms-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("itms");

// Redis holds cache, notification fan-out, and rate-limit counters. Nothing in it
// is a source of truth, so the volume is a convenience rather than a guarantee.
var redis = builder.AddRedis("redis")
    .WithImageTag("7.4")
    .WithDataVolume("itms-redis-data")
    .WithLifetime(ContainerLifetime.Persistent);

// MailHog captures outbound dev mail so nothing ever leaves the machine. It is
// deliberately ephemeral: a restart losing captured messages costs nothing, and a
// maildir volume would be one more thing to reason about.
var mailhog = builder.AddContainer("mailhog", "mailhog/mailhog", "v1.0.1")
    .WithEndpoint(targetPort: 1025, name: "smtp")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithHttpHealthCheck(path: "/", endpointName: "http");

var smtp = mailhog.GetEndpoint("smtp");

var webHost = builder.AddProject<Projects.Itms_Web_Host>("web-host")
    .WithReference(database)
    .WithReference(redis)
    .WaitFor(database)
    .WaitFor(redis)
    // SMTP has no Aspire connection-string convention, so the endpoint is bound to
    // the configuration keys the Notifications module will read.
    .WithEnvironment("Smtp__Host", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("Smtp__Port", smtp.Property(EndpointProperty.Port))
    .WaitFor(mailhog)
    .WithHttpHealthCheck("/health");

// The React shell (WP-0.8). It runs as the Vite dev server rather than as static files
// behind the host: `npm install` and the dev server are the two steps `aspire run` is
// meant to remove from a session, and hot reload is worth more in development than the
// production topology is.
//
// The reference gives the client the host's address as a service-discovery variable,
// which is what `vite.config.ts` proxies /api to. That proxy is deliberate: the session
// cookie is SameSite=Lax and same-origin, so a cross-origin client would need CORS.
builder.AddViteApp("web-client", "../Itms.Web.Client")
    .WithNpm()
    .WithReference(webHost)
    .WaitFor(webHost)
    .WithExternalHttpEndpoints();

builder.Build().Run();
