using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Persistence;

/// <summary>
/// The Helpdesk module's context: its own schema, its own migrations history, and no
/// table any other module may read (ARCHITECTURE.md §4).
/// </summary>
/// <remarks>
/// It is always built on the connection <c>IModuleDbSession</c> hands out, never on a
/// pool of its own, so a change here and any outbox write that announces it commit in
/// one transaction.
/// </remarks>
/// <param name="options">Context options, built on the shared session connection.</param>
public sealed class HelpdeskDbContext(DbContextOptions<HelpdeskDbContext> options) : DbContext(options)
{
    /// <summary>The name of the schema this context owns.</summary>
    public const string SchemaName = "helpdesk";

    /// <summary>The migrations history table, kept inside the helpdesk schema.</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>What a ticket is about.</summary>
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();

    /// <summary>How urgent a ticket is, and the targets that urgency promises.</summary>
    public DbSet<TicketPriority> TicketPriorities => Set<TicketPriority>();

    /// <summary>The requests for support themselves.</summary>
    public DbSet<Ticket> Tickets => Set<Ticket>();

    /// <summary>Every ticket's timeline: what moved, when, and at whose hand.</summary>
    public DbSet<TicketHistoryEntry> TicketHistory => Set<TicketHistoryEntry>();

    /// <summary>What was said about a ticket, publicly or inside the queue.</summary>
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();

    /// <summary>The files attached to a ticket. The rows only; the bytes live on disk.</summary>
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfiguration(new TicketCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new TicketPriorityConfiguration());
        modelBuilder.ApplyConfiguration(new TicketConfiguration());
        modelBuilder.ApplyConfiguration(new TicketHistoryEntryConfiguration());
        modelBuilder.ApplyConfiguration(new TicketCommentConfiguration());
        modelBuilder.ApplyConfiguration(new TicketAttachmentConfiguration());

        // No DbSet: the ticket-number counter is reached only by TicketNumberGenerator's
        // one statement, and applying its configuration directly is what keeps it that way.
        modelBuilder.ApplyConfiguration(new TicketNumberSequenceConfiguration());
    }
}
