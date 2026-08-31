using System.Diagnostics;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;
using Npgsql;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// WP-1.5's own done-criterion: "list queries project directly to DTOs, use no lazy
/// loading, and stay under 200ms on 50,000 seeded tickets".
/// </summary>
/// <remarks>
/// <para>
/// <b>The seed is a bulk <c>COPY</c>, not fifty thousand creations.</b> Creating them
/// through the endpoint would serialise on the number counter — WP-1.2 chose that
/// deliberately — and would take minutes for a fixture whose whole point is the read path.
/// The rows it writes are the same shape the create handler produces, numbers included.
/// </para>
/// <para>
/// <b>What this can and cannot prove.</b> It is a real query against a real PostgreSQL with
/// a realistic row count, so a projection that accidentally loads entities, an N+1, or a
/// missing index on a filter shows up here as a timing failure. It is not a benchmark:
/// container I/O on a developer's machine is noisy, and the threshold is generous enough
/// that only a structural mistake trips it. WP-6.4 owns the index review against the
/// measured query set.
/// </para>
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketQueuePerformanceTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    /// <summary>The row count WP-1.5's criterion names.</summary>
    private const int TicketCount = 50_000;

    /// <summary>The ceiling WP-1.5's criterion names.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(200);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Seeds once and measures every queue shape, because seeding fifty thousand rows per
    /// test method would put this one class over CONVENTIONS.md's two-minute budget for the
    /// whole suite.
    /// </summary>
    [Fact]
    public async Task Every_queue_shape_stays_inside_its_budget_on_fifty_thousand_tickets()
    {
        using var admin = await AuthClient.SignedInAsync(fixture, "admin", Token);
        using var user = await AuthClient.SignedInAsync(fixture, "user", Token);

        var world = await SeedAsync(admin);

        // The shapes WP-1.5 built: the default queue, each filter, each sort, and a deep page.
        string[] shapes =
        [
            string.Empty,
            "status=New",
            "status=New&status=Waiting",
            "unassigned=true",
            $"departmentId={world.DepartmentId}",
            $"requesterId={world.RequesterId}",
            "sort=Priority",
            "sort=UpdatedAt",
            "sort=DueAt",
            "sort=Number&direction=Ascending",
            "page=200&pageSize=100",

            // WP-1.8's shape: the "Overdue" view WP-1.9 will draw. It is the one filter
            // that compares two columns against the request's own instant rather than
            // against a constant, so it is the one worth measuring at volume.
            $"slaState={SlaState.Breached}",
            $"slaState={SlaState.Pending}",
        ];

        var slow = new List<string>();

        foreach (var shape in shapes)
        {
            // Warm first: the first call of each shape pays for its query plan, which is not
            // what the criterion is about.
            await TicketClient.ListAsync(admin, shape, Token);

            var elapsed = await MeasureAsync(admin, shape);

            if (elapsed >= Budget)
            {
                slow.Add($"'{(shape.Length == 0 ? "(no filters)" : shape)}' took {elapsed.TotalMilliseconds:F0} ms");
            }
        }

        // The row-level scope must not cost the criterion either: a requester's own queue is
        // one indexed predicate, and ix_tickets_requester_status exists for it.
        await TicketClient.ListAsync(user, string.Empty, Token);
        var scoped = await MeasureAsync(user, string.Empty);

        if (scoped >= Budget)
        {
            slow.Add($"the requester's own queue took {scoped.TotalMilliseconds:F0} ms");
        }

        // And the detail, which is a single indexed row read plus its timeline.
        await TicketClient.GetAsync(admin, world.SampleTicketId, Token);
        var stopwatch = Stopwatch.StartNew();
        await TicketClient.GetAsync(admin, world.SampleTicketId, Token);
        stopwatch.Stop();

        if (stopwatch.Elapsed >= Budget)
        {
            slow.Add($"the detail took {stopwatch.Elapsed.TotalMilliseconds:F0} ms");
        }

        slow.ShouldBeEmpty();

        // Proving the seed actually landed, so a silently empty table cannot pass this test.
        var page = await TicketClient.ListAsync(admin, string.Empty, Token);
        page.Total.ShouldBe(TicketCount);
        page.Items.Count.ShouldBe(25);
    }

    private static async Task<TimeSpan> MeasureAsync(HttpClient client, string query)
    {
        // The best of three: one slow container I/O spike should not fail a suite, while a
        // query that is structurally wrong is slow every time.
        var best = TimeSpan.MaxValue;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            await TicketClient.ListAsync(client, query, Token);
            stopwatch.Stop();

            if (stopwatch.Elapsed < best)
            {
                best = stopwatch.Elapsed;
            }
        }

        return best;
    }

    /// <summary>
    /// Writes <see cref="TicketCount"/> tickets straight into the table with a binary
    /// <c>COPY</c>, and moves the number counter past them so the module stays consistent.
    /// </summary>
    private async Task<SeededWorld> SeedAsync(HttpClient admin)
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);
        var departmentId = await TicketClient.DepartmentAsync(admin, "Water Operations", Token);
        var requesterId = await TicketClient.UserIdAsync(fixture, "user", Token);

        var statuses = new[]
        {
            TicketStatus.New, TicketStatus.Assigned, TicketStatus.InProgress,
            TicketStatus.Waiting, TicketStatus.Resolved, TicketStatus.Closed,
        };

        var now = DateTimeOffset.UtcNow;
        var sample = Guid.Empty;

        await using var connection = await fixture.DataSource.OpenConnectionAsync(Token);

        await using (var writer = await connection.BeginBinaryImportAsync(
            """
            COPY helpdesk.tickets (
                id, number, subject, description,
                requester_id, requester_name, department_id, department_name,
                category_id, priority_id, status,
                created_at, created_by, updated_at, updated_by,
                due_at, sla_response_due_at, sla_response_warn_at, sla_resolution_warn_at,
                sla_response_target_minutes, sla_resolution_target_minutes, sla_paused_total)
            FROM STDIN (FORMAT BINARY)
            """,
            Token))
        {
            for (var i = 1; i <= TicketCount; i++)
            {
                var id = Guid.CreateVersion7();
                if (i == 1)
                {
                    sample = id;
                }

                await writer.StartRowAsync(Token);
                await writer.WriteAsync(id, NpgsqlTypes.NpgsqlDbType.Uuid, Token);
                await writer.WriteAsync(TicketNumber.Format(i), NpgsqlTypes.NpgsqlDbType.Varchar, Token);
                await writer.WriteAsync($"Seeded ticket {i}", NpgsqlTypes.NpgsqlDbType.Varchar, Token);
                await writer.WriteAsync("Seeded for the WP-1.5 volume test.", NpgsqlTypes.NpgsqlDbType.Varchar, Token);
                await writer.WriteAsync(requesterId, NpgsqlTypes.NpgsqlDbType.Uuid, Token);
                await writer.WriteAsync("Dana Reyes", NpgsqlTypes.NpgsqlDbType.Varchar, Token);
                await writer.WriteAsync(departmentId, NpgsqlTypes.NpgsqlDbType.Uuid, Token);
                await writer.WriteAsync("Water Operations", NpgsqlTypes.NpgsqlDbType.Varchar, Token);
                await writer.WriteAsync(reference.CategoryId, NpgsqlTypes.NpgsqlDbType.Uuid, Token);
                await writer.WriteAsync(reference.PriorityId, NpgsqlTypes.NpgsqlDbType.Uuid, Token);
                await writer.WriteAsync(statuses[i % statuses.Length].ToString(), NpgsqlTypes.NpgsqlDbType.Varchar, Token);

                // Spread across a year, so a date-range filter and a sort have something to
                // discriminate on rather than fifty thousand identical instants.
                var createdAt = now.AddMinutes(-i);
                await writer.WriteAsync(createdAt, NpgsqlTypes.NpgsqlDbType.TimestampTz, Token);
                await writer.WriteAsync(DBNull.Value, Token);
                await writer.WriteAsync(createdAt, NpgsqlTypes.NpgsqlDbType.TimestampTz, Token);
                await writer.WriteAsync(DBNull.Value, Token);

                // WP-1.8's clocks, computed the way the entity computes them. Because the
                // creation instants are spread backwards a minute at a time, most of these
                // fifty thousand are already past their four-hour target — which is what
                // gives the overdue filter a realistic amount of work to do.
                var sla = TicketSla.Start(reference.Targets, createdAt);
                await writer.WriteAsync(sla.ResolutionDueAt, NpgsqlTypes.NpgsqlDbType.TimestampTz, Token);
                await writer.WriteAsync(sla.ResponseDueAt, NpgsqlTypes.NpgsqlDbType.TimestampTz, Token);
                await writer.WriteAsync(sla.ResponseWarnAt, NpgsqlTypes.NpgsqlDbType.TimestampTz, Token);
                await writer.WriteAsync(sla.ResolutionWarnAt, NpgsqlTypes.NpgsqlDbType.TimestampTz, Token);
                await writer.WriteAsync(sla.Targets.ResponseMinutes, NpgsqlTypes.NpgsqlDbType.Integer, Token);
                await writer.WriteAsync(sla.Targets.ResolutionMinutes, NpgsqlTypes.NpgsqlDbType.Integer, Token);
                await writer.WriteAsync(sla.PausedTotal, NpgsqlTypes.NpgsqlDbType.Interval, Token);
            }

            await writer.CompleteAsync(Token);
        }

        // ANALYZE, or the planner still believes the table is empty and picks a plan for
        // that — which would make this measure the wrong thing entirely.
        await using (var analyze = new NpgsqlCommand("ANALYZE helpdesk.tickets", connection))
        {
            await analyze.ExecuteNonQueryAsync(Token);
        }

        await using (var counter = new NpgsqlCommand(
            """
            INSERT INTO helpdesk.ticket_number_sequences (name, next_value)
            VALUES ('ticket', @next)
            ON CONFLICT (name) DO UPDATE SET next_value = @next
            """,
            connection))
        {
            counter.Parameters.AddWithValue("next", (long)TicketCount);
            await counter.ExecuteNonQueryAsync(Token);
        }

        return new SeededWorld(sample, departmentId, requesterId);
    }

    private sealed record SeededWorld(Guid SampleTicketId, Guid DepartmentId, Guid RequesterId);
}
