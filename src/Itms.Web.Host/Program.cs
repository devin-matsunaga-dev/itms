// Composition root only (ARCHITECTURE.md §2). Modules register themselves here and
// nowhere else; nothing in this file contains business logic.

using Itms.Messaging;
using Itms.Messaging.Abstractions;
using Itms.Modules.Directory;
using Itms.Modules.Directory.Seeding;
using Itms.Modules.Identity;
using Itms.Modules.Identity.Seeding;
using Itms.Platform;
using Itms.Platform.Data;
using Itms.Web.Host.Data;

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
// cannot reference a module, so the composition root is what names them. Identity has
// no consumer yet — the audit spine (WP-0.7) adds the first one.
builder.Services.AddMessaging(builder.Configuration);

// A module may not reference the bus, but every module context has to be built on the
// one connection the bus's session owns, or a module write and its outbox event would
// commit separately. The host is the only place that can see both sides.
builder.Services.AddScoped<IModuleDbSession, ModuleDbSessionAdapter>();

// Modules. Each contributes exactly one AddXxxModule here and one MapXxxEndpoints below.
builder.Services.AddIdentityModule();
builder.Services.AddDirectoryModule();

var app = builder.Build();

// Development applies its own migrations and seed data so `aspire run` is the only
// setup step. Production applies migrations as a deliberate deployment action, never
// on startup; the seeder itself refuses to create accounts outside Development.
if (app.Environment.IsDevelopment())
{
    await using var startupScope = app.Services.CreateAsyncScope();
    await startupScope.ServiceProvider.MigrateMessagingAsync();
    await startupScope.ServiceProvider.MigrateIdentityAsync();
    await startupScope.ServiceProvider.MigrateDirectoryAsync();
    await DevelopmentIdentitySeeder.SeedAsync(startupScope.ServiceProvider);
    await DevelopmentDirectorySeeder.SeedAsync(startupScope.ServiceProvider);
}

// ARCHITECTURE.md §6: errors are ProblemDetails, always. These two turn the
// responses no handler produced — an unhandled exception, a 404 from routing, a 401
// from the auth middleware — into problem documents as well.
app.UseExceptionHandler();
app.UseStatusCodePages();

// Before authorization, so a policy has a principal to evaluate. The cookie handler
// checks the session row on every request, which is what makes revocation immediate.
app.UseAuthentication();
app.UseAuthorization();

// Rate limits on the credential endpoints (CONVENTIONS.md's security floor). The
// policy itself is declared by the Identity module.
app.UseRateLimiter();

// /health (all checks) and /alive (liveness only), Development-only by default.
app.MapDefaultEndpoints();

app.MapGet("/", () => Results.Ok(new { service = "Itms.Web.Host", state = "skeleton" }));

app.MapIdentityEndpoints();
app.MapDirectoryEndpoints();

app.Run();
