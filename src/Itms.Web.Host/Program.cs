// Composition root only (ARCHITECTURE.md §2). Module registration and authentication
// (WP-0.5) each arrive with their own package.

using Itms.Messaging;
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

// The in-process bus and its transactional outbox (ARCHITECTURE.md §5). Registered
// before any AddXxxModule, because modules take IEventPublisher from here. The
// assemblies passed are the ones scanned for IEventConsumer implementations; the bus
// cannot reference a module, so the composition root is what names them. No module
// has consumers yet, so the list is empty until Phase 1.
builder.Services.AddMessaging(builder.Configuration);

var app = builder.Build();

// Development applies its own migrations so `aspire run` is the only setup step.
// Production applies them as a deliberate deployment action, never on startup.
if (app.Environment.IsDevelopment())
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    await migrationScope.ServiceProvider.MigrateMessagingAsync();
}

// ARCHITECTURE.md §6: errors are ProblemDetails, always. These two turn the
// responses no handler produced — an unhandled exception, a 404 from routing, a 401
// from the auth middleware — into problem documents as well.
app.UseExceptionHandler();
app.UseStatusCodePages();

// /health (all checks) and /alive (liveness only), Development-only by default.
app.MapDefaultEndpoints();

app.MapGet("/", () => Results.Ok(new { service = "Itms.Web.Host", state = "skeleton" }));

app.Run();
