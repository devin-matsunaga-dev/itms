// Composition root only (ARCHITECTURE.md §2). Aspire wiring and health endpoints
// arrive in WP-0.2, the shared kernel in WP-0.3, authentication in WP-0.5, and
// module registration with each module's own package. Today this host starts and
// answers one probe, which is what WP-0.1 asks of it.

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "Itms.Web.Host", state = "skeleton" }));

app.Run();
