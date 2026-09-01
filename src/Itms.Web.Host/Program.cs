// Composition root only (ARCHITECTURE.md §2). Modules register themselves here and
// nowhere else; nothing in this file contains business logic.

using Itms.Messaging;
using Itms.Messaging.Abstractions;
using Itms.Modules.Assets;
using Itms.Modules.Assets.Seeding;
using Itms.Modules.Audit;
using Itms.Modules.Directory;
using Itms.Modules.Directory.Seeding;
using Itms.Modules.Helpdesk;
using Itms.Modules.Helpdesk.Seeding;
using Itms.Modules.Identity;
using Itms.Modules.Identity.Seeding;
using Itms.Platform;
using Itms.Platform.Data;
using Itms.Web.Host.Data;
using Itms.Web.Host.OpenApi;

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
// cannot reference a module, so the composition root is what names them. A module that
// adds a consumer and is not named here is a consumer that silently never runs, so
// every future module joins this list as it gains one.
builder.Services.AddMessaging(
    builder.Configuration,
    typeof(Itms.Modules.Audit.AssemblyMarker).Assembly);

// A module may not reference the bus, but every module context has to be built on the
// one connection the bus's session owns, or a module write and its outbox event would
// commit separately. The host is the only place that can see both sides.
builder.Services.AddScoped<IModuleDbSession, ModuleDbSessionAdapter>();

// The API contract (ARCHITECTURE.md §6). Registered before the modules so the document
// is in place by the time their endpoints are mapped; the document itself is generated
// from that endpoint metadata, written to openapi/v1.json at build, and the React
// client's types are generated from that file.
builder.Services.AddItmsOpenApi();

// Modules. Each contributes exactly one AddXxxModule here and one MapXxxEndpoints below.
builder.Services.AddIdentityModule();
builder.Services.AddDirectoryModule();
builder.Services.AddHelpdeskModule();
builder.Services.AddAssetsModule();
builder.Services.AddAuditModule();

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
    await startupScope.ServiceProvider.MigrateHelpdeskAsync();
    await startupScope.ServiceProvider.MigrateAssetsAsync();
    await startupScope.ServiceProvider.MigrateAuditAsync();
    await DevelopmentIdentitySeeder.SeedAsync(startupScope.ServiceProvider);
    await DevelopmentDirectorySeeder.SeedAsync(startupScope.ServiceProvider);
    // Reference data rather than demo data: unlike the development directory this seeder
    // runs in every environment, and the deployment step that applies migrations must run
    // it too — a deployment with no ticket priorities could not accept a ticket. It is
    // idempotent and keyed on fixed ids, so a restart adds nothing and overwrites nothing
    // an administrator has changed since. It sits inside this block because seeding on
    // every start would also run during build-time OpenAPI generation, which boots the
    // host with no database at all.
    await HelpdeskReferenceDataSeeder.SeedAsync(startupScope.ServiceProvider);
    // Same shape, same obligation, and the same gap: a deployment with no asset statuses
    // could not record an asset. WP-6.6 owns the one first-run step that runs every
    // reference-data seeder — this deliberately joins that gap rather than inventing a
    // second mechanism.
    await AssetsReferenceDataSeeder.SeedAsync(startupScope.ServiceProvider);
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

// Excluded from the document deliberately: the contract describes the API, and the API
// is everything under /api/v1. A root probe left in it would be the one operation a
// generated client has no use for.
app.MapGet("/", () => Results.Ok(new { service = "Itms.Web.Host", state = "skeleton" }))
    .ExcludeFromDescription();

// The document is served in Development so it can be read against a running host; the
// committed copy at openapi/v1.json is what tooling and CI use, and is written at build.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapIdentityEndpoints();
app.MapDirectoryEndpoints();
app.MapHelpdeskEndpoints();
app.MapAssetsEndpoints();
// Audit maps nothing today; the trail is read by WP-5.9's viewer. The call is here so
// adding that viewer is an edit inside the module rather than a change to this file.
app.MapAuditEndpoints();

app.Run();
