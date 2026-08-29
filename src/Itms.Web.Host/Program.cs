// Composition root only (ARCHITECTURE.md §2). Module registration, the outbox
// (WP-0.4), and authentication (WP-0.5) each arrive with their own package.

using Itms.Platform;

var builder = WebApplication.CreateBuilder(args);

// Service discovery, resilience, health checks, and OpenTelemetry.
builder.AddServiceDefaults();

// Connection strings come from Aspire under these resource names. Nothing here
// falls back to a literal: if the AppHost has not supplied them, startup fails
// loudly rather than quietly talking to the wrong database.
builder.AddNpgsqlDataSource("itms");
builder.AddRedisClient("redis");

// The shared kernel: the clock, the current-user accessor, and RFC 7807 problem
// details. Registered before any AddXxxModule, because every module depends on it.
builder.Services.AddPlatform();

var app = builder.Build();

// ARCHITECTURE.md §6: errors are ProblemDetails, always. These two turn the
// responses no handler produced — an unhandled exception, a 404 from routing, a 401
// from the auth middleware — into problem documents as well.
app.UseExceptionHandler();
app.UseStatusCodePages();

// /health (all checks) and /alive (liveness only), Development-only by default.
app.MapDefaultEndpoints();

app.MapGet("/", () => Results.Ok(new { service = "Itms.Web.Host", state = "skeleton" }));

app.Run();
