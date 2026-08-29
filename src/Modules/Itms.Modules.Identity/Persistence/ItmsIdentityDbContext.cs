using Itms.Modules.Identity.Domain;
using Itms.Modules.Identity.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Identity.Persistence;

/// <summary>
/// The Identity module's context: its own schema, its own migrations history, and no
/// table any other module may read (ARCHITECTURE.md §4).
/// </summary>
/// <remarks>
/// It is always built on the connection <c>IModuleDbSession</c> hands out, never on a
/// pool of its own, so a change here and the outbox write that announces it commit in
/// one transaction.
/// </remarks>
/// <param name="options">Context options, built on the shared session connection.</param>
public sealed class ItmsIdentityDbContext(DbContextOptions<ItmsIdentityDbContext> options)
    : IdentityDbContext<ItmsUser, ItmsRole, Guid>(options)
{
    /// <summary>The name of the schema this context owns.</summary>
    public const string SchemaName = "identity";

    /// <summary>The migrations history table, kept inside the identity schema.</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>Live and historical sign-ins. The row the cookie points at.</summary>
    public DbSet<UserSession> Sessions => Set<UserSession>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // The base call contributes the Identity keys and relationships; everything
        // after it renames what the base named, so the schema follows CONVENTIONS.md's
        // snake_case rule rather than the framework's PascalCase defaults.
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(SchemaName);

        builder.ApplyConfiguration(new UserConfiguration());
        builder.ApplyConfiguration(new RoleConfiguration());
        builder.ApplyConfiguration(new UserSessionConfiguration());
        builder.ApplyConfiguration(new UserRoleConfiguration());
        builder.ApplyConfiguration(new UserClaimConfiguration());
        builder.ApplyConfiguration(new RoleClaimConfiguration());
        builder.ApplyConfiguration(new UserLoginConfiguration());
        builder.ApplyConfiguration(new UserTokenConfiguration());
    }
}
