using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.TicketHistory;
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
    /// <b>Why this bypasses the entity, and what WP-1.6 took back from it.</b> Reaching
    /// <c>Assigned</c> no longer needs this — <see cref="AssignAsync"/> does it through
    /// the entity — and <c>TicketStatusEndpointTests</c> now starts its walks there. What
    /// this is still for is the states <em>beyond</em> Assigned: reaching Waiting or
    /// Resolved legitimately means walking a chain of transitions that are themselves
    /// under test elsewhere, and arranging one test by exercising another is how a suite
    /// stops telling you which thing broke.
    /// </para>
    /// <para>
    /// It is test <em>arrangement</em>, never the thing under test: every assertion still
    /// makes its transition through the real endpoint. This is the same move WP-1.2 made
    /// when it attacked the foreign keys and the soft-delete filter with plain SQL.
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

    /// <summary>Every history entry a ticket has, oldest first, read straight from the table.</summary>
    /// <remarks>
    /// Read off the row rather than through the endpoint, so a test can assert that a
    /// rolled-back transaction wrote nothing without the read path being able to hide it.
    /// </remarks>
    /// <param name="services">The host's provider.</param>
    /// <param name="ticketId">The ticket whose timeline is wanted.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entries, oldest first.</returns>
    public static async Task<IReadOnlyList<TicketHistoryEntry>> HistoryAsync(
        IServiceProvider services,
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();

        return await database.TicketHistory
            .AsNoTracking()
            .Where(entry => entry.TicketId == ticketId)
            .OrderBy(entry => entry.OccurredAt)
            .ThenBy(entry => entry.Sequence)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Moves a ticket the way <c>ChangeTicketStatusHandler</c> does — snapshot, transition,
    /// record, save — inside one transaction the caller can make fail.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the shape of the handler, not a rehearsal of it: it loads the ticket tracked
    /// on the session's own connection, takes the before-snapshot, calls the entity, and
    /// hands the real <see cref="TicketHistoryRecorder"/> the result, all inside one
    /// <c>IModuleDbSession</c> transaction. WP-1.2 wrote its numbering tests against a
    /// helper of exactly this shape, for exactly this reason.
    /// </para>
    /// <para>
    /// It exists because invariant 3's rollback half cannot be reached through the endpoint:
    /// the handler opens and commits its own transaction, so a test that only speaks HTTP
    /// has no moment at which to make it fail. Every other assertion about history still
    /// goes through the real endpoint.
    /// </para>
    /// </remarks>
    /// <param name="services">The host's provider.</param>
    /// <param name="ticketId">The ticket to move.</param>
    /// <param name="target">The status to move it to.</param>
    /// <param name="resolutionNotes">The resolution, when moving to Resolved.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <param name="failAfterSave">
    /// Runs inside the transaction after the change and its history have been saved. A test
    /// that throws from here rolls both of them back.
    /// </param>
    public static async Task MoveAsync(
        IServiceProvider services,
        Guid ticketId,
        TicketStatus target,
        string? resolutionNotes,
        CancellationToken cancellationToken,
        Action? failAfterSave = null)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var database = provider.GetRequiredService<HelpdeskDbContext>();
        var session = provider.GetRequiredService<IModuleDbSession>();
        var history = provider.GetRequiredService<TicketHistoryRecorder>();
        var clock = provider.GetRequiredService<IClock>();

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token);

                var ticket = await database.Tickets.SingleAsync(candidate => candidate.Id == ticketId, token);
                var before = TicketSnapshot.Of(ticket);
                var now = clock.UtcNow;

                var transition = ticket.ChangeStatus(target, resolutionNotes, now, actor: null);
                if (transition.IsFailure)
                {
                    throw new InvalidOperationException($"The suite asked for an illegal move to {target}.");
                }

                await history.RecordAsync(ticket, before, now, token);
                await database.SaveChangesAsync(token);

                failAfterSave?.Invoke();
            },
            cancellationToken);
    }

    /// <summary>
    /// Assigns a ticket the way <c>AssignTicketHandler</c> does — snapshot, entity call,
    /// record, save — so a starting state past <c>New</c> is reached by the production
    /// path rather than by writing the status column.
    /// </summary>
    /// <remarks>
    /// It stops short of publishing. A suite arranging a starting state does not want two
    /// audit rows about the arrangement turning up in the trail it is asserting on, and
    /// <c>TicketAssignmentEndpointTests</c> is where the events themselves are proved.
    /// </remarks>
    /// <param name="services">The host's provider.</param>
    /// <param name="ticketId">The ticket to assign.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <param name="assigneeId">Who takes it on. Defaults to a stable fake technician.</param>
    /// <param name="assigneeName">Their display name.</param>
    public static async Task AssignAsync(
        IServiceProvider services,
        Guid ticketId,
        CancellationToken cancellationToken,
        Guid? assigneeId = null,
        string assigneeName = "Priya Raman")
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var database = provider.GetRequiredService<HelpdeskDbContext>();
        var session = provider.GetRequiredService<IModuleDbSession>();
        var history = provider.GetRequiredService<TicketHistoryRecorder>();
        var clock = provider.GetRequiredService<IClock>();

        await session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token);

                var ticket = await database.Tickets.SingleAsync(candidate => candidate.Id == ticketId, token);
                var before = TicketSnapshot.Of(ticket);
                var now = clock.UtcNow;

                var assigned = ticket.Assign(assigneeId ?? StandInTechnician, assigneeName, now, actor: null);
                if (assigned.IsFailure)
                {
                    throw new InvalidOperationException(
                        $"The suite could not assign ticket {ticketId}: {assigned.Error!.Message}");
                }

                await history.RecordAsync(ticket, before, now, token);
                await database.SaveChangesAsync(token);
            },
            cancellationToken);
    }

    /// <summary>
    /// The technician <see cref="AssignAsync"/> hands tickets to by default.
    /// </summary>
    /// <remarks>
    /// A fixed id rather than a fresh one per call, so two arrangements in one test assign
    /// to the same person and a reassignment in a test is visibly a different id. It is
    /// not a real account: the entity does not check, and the endpoint tests use the
    /// seeded ones where it does.
    /// </remarks>
    public static readonly Guid StandInTechnician = new("0199a2c1-0000-7000-8000-000000000001");

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
