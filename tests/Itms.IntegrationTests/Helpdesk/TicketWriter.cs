using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// Raises a ticket through the module's real plumbing — its own scope, its own
/// connection, its own transaction — because WP-1.2 ships no endpoint to do it with.
/// </summary>
/// <remarks>
/// This is deliberately the shape WP-1.5's create handler will have: claim the number
/// inside the transaction, build the entity, save, commit. Writing the tests against it
/// means the numbering is proved against the way it will actually be called rather than
/// against a rehearsal of it.
/// </remarks>
internal static class TicketWriter
{
    /// <summary>The category and priority a test files its tickets against.</summary>
    /// <param name="CategoryId">A seeded category.</param>
    /// <param name="PriorityId">A seeded priority.</param>
    public readonly record struct ReferenceData(Guid CategoryId, Guid PriorityId);

    /// <summary>Reads a seeded category and priority to file tickets against.</summary>
    /// <param name="services">The host's provider.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The ids of the first active category and priority.</returns>
    public static async Task<ReferenceData> ReferenceDataAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();

        var categoryId = await database.TicketCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Id)
            .FirstAsync(cancellationToken);

        var priorityId = await database.TicketPriorities
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Rank)
            .Select(p => p.Id)
            .FirstAsync(cancellationToken);

        return new ReferenceData(categoryId, priorityId);
    }

    /// <summary>A draft ticket for <paramref name="reference"/>, with a distinguishable subject.</summary>
    /// <param name="reference">The category and priority to file against.</param>
    /// <param name="subject">The subject line.</param>
    /// <returns>The draft.</returns>
    public static NewTicket Draft(ReferenceData reference, string subject = "Laptop will not charge") => new(
        subject,
        "It stops charging at 40% and the light goes amber.",
        Guid.CreateVersion7(),
        "Dana Reyes",
        Guid.CreateVersion7(),
        "Water Operations",
        reference.CategoryId,
        reference.PriorityId);

    /// <summary>
    /// Raises one ticket in its own scope and transaction.
    /// </summary>
    /// <param name="services">The host's provider.</param>
    /// <param name="draft">The ticket's fields.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <param name="failAfterSave">
    /// Runs inside the transaction after the ticket has been saved. A test that throws
    /// from here rolls the whole creation back, number included.
    /// </param>
    /// <returns>The ticket, as committed.</returns>
    public static async Task<Ticket> CreateAsync(
        IServiceProvider services,
        NewTicket draft,
        CancellationToken cancellationToken,
        Action? failAfterSave = null)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var database = provider.GetRequiredService<HelpdeskDbContext>();
        var session = provider.GetRequiredService<IModuleDbSession>();
        var numbers = provider.GetRequiredService<TicketNumberGenerator>();
        var clock = provider.GetRequiredService<IClock>();

        Ticket? created = null;

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token);

                var number = await numbers.ClaimAsync(token);
                var ticket = Ticket.Create(number, draft, clock.UtcNow, actor: null);

                database.Tickets.Add(ticket);
                await database.SaveChangesAsync(token);

                failAfterSave?.Invoke();
                created = ticket;
            },
            cancellationToken);

        return created!;
    }

    /// <summary>
    /// Puts an existing ticket into <paramref name="status"/> by writing the column
    /// directly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this bypasses the entity.</b> Every state past <c>New</c> is reached by
    /// first assigning the ticket to somebody, and assignment is WP-1.6 — so until that
    /// package lands there is no legitimate route to <c>Assigned</c> and therefore none to
    /// anything beyond it. A suite that could only reach <c>New</c> could not assert the
    /// state machine over the wire at all, which is what WP-1.3 is required to do.
    /// </para>
    /// <para>
    /// It is test <em>arrangement</em>, never the thing under test: every assertion still
    /// makes its transition through the real endpoint. This is the same move WP-1.2 made
    /// when it attacked the foreign keys and the soft-delete filter with plain SQL.
    /// <b>When WP-1.6 arrives, the walks that start here should start from a real
    /// assignment instead.</b>
    /// </para>
    /// </remarks>
    /// <param name="dataSource">The test database.</param>
    /// <param name="ticketId">The ticket to park.</param>
    /// <param name="status">The status to park it in.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public static async Task ParkAsync(
        NpgsqlDataSource dataSource,
        Guid ticketId,
        TicketStatus status,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        // Resolved and Closed carry an instant and a resolution in production, so parking
        // has to leave the row in a shape the real transitions would have produced —
        // otherwise a reopen would be asserted against a state that cannot occur.
        const string Sql = """
            UPDATE helpdesk.tickets
               SET status = @status,
                   resolution_notes = CASE WHEN @status IN ('Resolved', 'Closed')
                                           THEN 'Parked by the test suite.' ELSE NULL END,
                   resolved_at = CASE WHEN @status IN ('Resolved', 'Closed')
                                      THEN now() AT TIME ZONE 'utc' ELSE NULL END,
                   closed_at = CASE WHEN @status = 'Closed'
                                    THEN now() AT TIME ZONE 'utc' ELSE NULL END
             WHERE id = @id
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(Sql, connection);
        command.Parameters.AddWithValue("status", status.ToString());
        command.Parameters.AddWithValue("id", ticketId);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected != 1)
        {
            throw new InvalidOperationException($"No ticket {ticketId} to park in {status}.");
        }
    }

    /// <summary>Reads a ticket's status and resolution fields straight from the row.</summary>
    /// <param name="services">The host's provider.</param>
    /// <param name="ticketId">The ticket to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The persisted state, so a test can assert a refused transition moved nothing.</returns>
    public static async Task<(TicketStatus Status, DateTimeOffset? ResolvedAt, DateTimeOffset? ClosedAt, string? Notes)>
        StateAsync(IServiceProvider services, Guid ticketId, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();

        return await database.Tickets
            .AsNoTracking()
            .Where(t => t.Id == ticketId)
            .Select(t => new ValueTuple<TicketStatus, DateTimeOffset?, DateTimeOffset?, string?>(
                t.Status, t.ResolvedAt, t.ClosedAt, t.ResolutionNotes))
            .SingleAsync(cancellationToken);
    }

    /// <summary>The numbers of every ticket in the database, in issue order.</summary>
    /// <param name="services">The host's provider.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Every ticket number, ascending.</returns>
    public static async Task<IReadOnlyList<string>> NumbersAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();

        return await database.Tickets
            .AsNoTracking()
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.Number)
            .Select(t => t.Number)
            .ToListAsync(cancellationToken);
    }
}
