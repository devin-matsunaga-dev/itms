// Aspire orchestration. PostgreSQL, Redis, MailHog, and the Web.Host resource are
// wired in WP-0.2 — this AppHost currently builds an empty distributed
// application so the project exists and compiles.

var builder = DistributedApplication.CreateBuilder(args);

builder.Build().Run();
