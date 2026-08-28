// Composition root only (ARCHITECTURE.md §2). Module registration, the shared
// kernel (WP-0.3), the outbox (WP-0.4), and authentication (WP-0.5) each arrive
// with their own package. Today this host proves the Aspire-supplied connection
// strings reach it and that /health reflects the backing services.

var builder = WebApplication.CreateBuilder(args);

// Service discovery, resilience, health checks, and OpenTelemetry.
builder.AddServiceDefaults();

// Connection strings come from Aspire under these resource names. Nothing here
// falls back to a literal: if the AppHost has not supplied them, startup fails
// loudly rather than quietly talking to the wrong database.
builder.AddNpgsqlDataSource("itms");
builder.AddRedisClient("redis");

var app = builder.Build();

// /health (all checks) and /alive (liveness only), Development-only by default.
app.MapDefaultEndpoints();

app.MapGet("/", () => Results.Ok(new { service = "Itms.Web.Host", state = "skeleton" }));

app.Run();
