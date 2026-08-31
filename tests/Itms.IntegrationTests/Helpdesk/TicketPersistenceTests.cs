using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// The ticket row against a real PostgreSQL: what the column set holds, what the database
/// refuses, and the two mappings that are invisible in C# — the xmin concurrency token and
/// the soft-delete query filter.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketPersistenceTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_ticket_round_trips_every_field_it_was_created_with()
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var draft = TicketWriter.Draft(reference, "Badge reader offline at the north gate");

        var created = await TicketWriter.CreateAsync(fixture.Services, draft, Token);

        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
        var stored = await database.Tickets.AsNoTracking().SingleAsync(t => t.Id == created.Id, Token);

        stored.Number.ShouldBe(created.Number);
        stored.Subject.ShouldBe(draft.Subject);
        stored.Description.ShouldBe(draft.Description);
        stored.RequesterId.ShouldBe(draft.RequesterId);
        stored.RequesterName.ShouldBe(draft.RequesterName);
        stored.DepartmentId.ShouldBe(draft.DepartmentId);
        stored.DepartmentName.ShouldBe(draft.DepartmentName);
        stored.CategoryId.ShouldBe(reference.CategoryId);
        stored.PriorityId.ShouldBe(reference.PriorityId);
        stored.Status.ShouldBe(TicketStatus.New);
        // PostgreSQL stores timestamptz to the microsecond and .NET counts in 100ns
        // ticks, so the round trip is exact to a microsecond and no finer.
        stored.CreatedAt.ShouldBe(created.CreatedAt, TimeSpan.FromMicroseconds(1));
    }

    /// <summary>
    /// The status is stored as text, not as an ordinal, so a row read at a psql prompt
    /// during an incident says what it means.
    /// </summary>
    [Fact]
    public async Task The_status_is_stored_as_text()
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var created = await TicketWriter.CreateAsync(fixture.Services, TicketWriter.Draft(reference), Token);

        var status = await ScalarAsync<string>(
            "SELECT status FROM helpdesk.tickets WHERE id = @id",
            ("id", created.Id));

        status.ShouldBe(nameof(TicketStatus.New));
    }

    /// <summary>
    /// People quote the number on the phone. Two tickets answering to one would be the
    /// worst ambiguity this system could have, so the database refuses rather than
    /// trusting the generator to be the only writer.
    /// </summary>
    [Fact]
    public async Task Two_tickets_cannot_share_a_number()
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var created = await TicketWriter.CreateAsync(fixture.Services, TicketWriter.Draft(reference), Token);

        var duplicate = await Should.ThrowAsync<PostgresException>(
            () => ExecuteAsync(
                """
                INSERT INTO helpdesk.tickets
                    (id, number, subject, description, requester_id, requester_name,
                     department_id, department_name, category_id, priority_id, status,
                     created_at, updated_at)
                SELECT gen_random_uuid(), number, subject, description, requester_id, requester_name,
                       department_id, department_name, category_id, priority_id, status,
                       created_at, updated_at
                FROM helpdesk.tickets WHERE id = @id
                """,
                ("id", created.Id)));

        duplicate.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        duplicate.ConstraintName.ShouldBe("ux_tickets_number");
    }

    /// <summary>
    /// WP-1.1 answered "deleting one in use is blocked" by shipping no removal path at
    /// all. This is the other half, and the stronger one: with a ticket filed against it,
    /// the database itself refuses the delete even from a psql prompt.
    /// </summary>
    [Fact]
    public async Task A_category_a_ticket_is_filed_against_cannot_be_deleted()
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        await TicketWriter.CreateAsync(fixture.Services, TicketWriter.Draft(reference), Token);

        var refused = await Should.ThrowAsync<PostgresException>(
            () => ExecuteAsync(
                "DELETE FROM helpdesk.ticket_categories WHERE id = @id",
                ("id", reference.CategoryId)));

        refused.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
        refused.ConstraintName.ShouldBe("fk_tickets_category_id");
    }

    [Fact]
    public async Task A_priority_a_ticket_is_filed_against_cannot_be_deleted()
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        await TicketWriter.CreateAsync(fixture.Services, TicketWriter.Draft(reference), Token);

        var refused = await Should.ThrowAsync<PostgresException>(
            () => ExecuteAsync(
                "DELETE FROM helpdesk.ticket_priorities WHERE id = @id",
                ("id", reference.PriorityId)));

        refused.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
        refused.ConstraintName.ShouldBe("fk_tickets_priority_id");
    }

    /// <summary>
    /// Renaming a category reaches every ticket already filed under it, because the ticket
    /// holds the id and nothing else. That is legal here — §3 rule 6 forbids the foreign
    /// key only across a module boundary — and it is what WP-1.1's criterion asked for.
    /// </summary>
    [Fact]
    public async Task A_renamed_category_is_still_the_one_the_ticket_points_at()
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var created = await TicketWriter.CreateAsync(fixture.Services, TicketWriter.Draft(reference), Token);

        using var admin = await SignedInAsync("admin");
        var response = await ApiClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{HelpdeskClient.Categories}/{reference.CategoryId}",
            new { name = "Hardware & Peripherals", description = (string?)null, sortOrder = 10 },
            Token);
        response.EnsureSuccessStatusCode();

        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();

        var stored = await database.Tickets.AsNoTracking().SingleAsync(t => t.Id == created.Id, Token);
        stored.CategoryId.ShouldBe(reference.CategoryId);

        var category = await database.TicketCategories.AsNoTracking()
            .SingleAsync(c => c.Id == reference.CategoryId, Token);
        category.Name.ShouldBe("Hardware & Peripherals");
    }

    /// <summary>
    /// Nothing soft-deletes a ticket yet, which is exactly why the filter goes in now:
    /// added later it would silently change every list query written in the meantime.
    /// The row is marked with SQL because the entity offers no way to do it.
    /// </summary>
    [Fact]
    public async Task A_soft_deleted_ticket_is_invisible_unless_the_filter_is_ignored()
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var created = await TicketWriter.CreateAsync(fixture.Services, TicketWriter.Draft(reference), Token);

        await ExecuteAsync("UPDATE helpdesk.tickets SET deleted_at = now() WHERE id = @id", ("id", created.Id));

        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();

        (await database.Tickets.AsNoTracking().AnyAsync(t => t.Id == created.Id, Token)).ShouldBeFalse();
        (await database.Tickets.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(t => t.Id == created.Id, Token)).DeletedAt.ShouldNotBeNull();
    }

    /// <summary>
    /// ARCHITECTURE.md §6 wants optimistic concurrency on tickets, and this is the mapping
    /// it rests on: xmin is PostgreSQL's own row version, it is read back on insert, and it
    /// moves whenever the row does. Turning it into an ETag and a 409 is WP-1.5's; a
    /// mutation to race against arrives with WP-1.3.
    /// </summary>
    [Fact]
    public async Task The_row_version_is_populated_on_insert_and_moves_when_the_row_changes()
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var created = await TicketWriter.CreateAsync(fixture.Services, TicketWriter.Draft(reference), Token);

        var before = await RowVersionAsync(created.Id);
        before.ShouldNotBe(0u);

        await ExecuteAsync(
            "UPDATE helpdesk.tickets SET subject = 'Changed underneath' WHERE id = @id",
            ("id", created.Id));

        (await RowVersionAsync(created.Id)).ShouldNotBe(before);
    }

    private async Task<HttpClient> SignedInAsync(string userName)
    {
        var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        return client;
    }

    private async Task<uint> RowVersionAsync(Guid ticketId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();

        var ticket = await database.Tickets.SingleAsync(t => t.Id == ticketId, Token);
        return database.Entry(ticket).Property<uint>("Version").CurrentValue;
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync(Token);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync(Token))!;
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync(Token);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(Token);
    }
}
