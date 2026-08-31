using Itms.IntegrationTests.Identity;
using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Itms.IntegrationTests.Helpdesk;

/// <summary>
/// WP-1.2's own criterion: five hundred concurrent creations produce five hundred unique
/// sequential numbers with no gaps or collisions.
/// </summary>
/// <remarks>
/// <para>
/// This is the package's whole reason for using a counter row rather than a PostgreSQL
/// sequence. A sequence would satisfy "unique" and "no collisions" trivially and fail
/// "no gaps" the first time a creation rolled back, which is why the rollback case below
/// is as much the point as the concurrent one.
/// </para>
/// <para>
/// The fan-out is bounded rather than five hundred tasks at once because every creation
/// takes a scope, and a scope takes a connection: five hundred at once would exhaust the
/// pool and PostgreSQL's connection limit and prove nothing about numbering. Thirty-two
/// racing writers against one counter row is the contention the row lock has to survive.
/// </para>
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class TicketNumberingTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private const int Tickets = 500;
    private const int Writers = 32;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Five_hundred_concurrent_creations_produce_five_hundred_unbroken_numbers()
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);

        await Parallel.ForEachAsync(
            Enumerable.Range(1, Tickets),
            new ParallelOptions { MaxDegreeOfParallelism = Writers, CancellationToken = Token },
            async (index, token) =>
            {
                var draft = TicketWriter.Draft(reference, $"Concurrent creation {index}");
                await TicketWriter.CreateAsync(fixture.Services, draft, token);
            });

        var numbers = await TicketWriter.NumbersAsync(fixture.Services, Token);

        numbers.Count.ShouldBe(Tickets);
        numbers.Distinct(StringComparer.Ordinal).Count().ShouldBe(Tickets);
        numbers.Order(StringComparer.Ordinal).ShouldBe(Expected(1, Tickets));
    }

    /// <summary>
    /// The counter is claimed inside the caller's transaction, so a creation that fails
    /// gives its number back. A sequence could not do this, and the numbering would show
    /// a hole where the failure was.
    /// </summary>
    [Fact]
    public async Task A_creation_that_rolls_back_leaves_no_gap()
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);

        var first = await TicketWriter.CreateAsync(fixture.Services, TicketWriter.Draft(reference), Token);
        first.Number.ShouldBe("TKT-0001");

        await Should.ThrowAsync<InvalidOperationException>(
            () => TicketWriter.CreateAsync(
                fixture.Services,
                TicketWriter.Draft(reference, "Doomed"),
                Token,
                failAfterSave: () => throw new InvalidOperationException("Something after the save went wrong.")));

        var third = await TicketWriter.CreateAsync(fixture.Services, TicketWriter.Draft(reference), Token);

        third.Number.ShouldBe("TKT-0002");
        (await TicketWriter.NumbersAsync(fixture.Services, Token)).ShouldBe(["TKT-0001", "TKT-0002"]);
    }

    /// <summary>
    /// A number claimed outside a transaction could not be given back, so the generator
    /// refuses rather than issuing one — the same call <c>IEventPublisher</c> makes about
    /// publishing outside a transaction, for the same reason.
    /// </summary>
    [Fact]
    public async Task Claiming_outside_a_transaction_is_refused()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var numbers = scope.ServiceProvider.GetRequiredService<TicketNumberGenerator>();

        scope.ServiceProvider.GetRequiredService<IModuleDbSession>().CurrentTransaction.ShouldBeNull();

        await Should.ThrowAsync<InvalidOperationException>(() => numbers.ClaimAsync(Token));
    }

    /// <summary>
    /// Nothing seeds the counter row. A fresh database — and a database Respawn has just
    /// truncated — both start at one, which is what keeps the numbering out of the
    /// deployment step that has to run the reference-data seeder.
    /// </summary>
    [Fact]
    public async Task A_database_that_has_never_issued_a_number_starts_at_one()
    {
        var reference = await TicketWriter.ReferenceDataAsync(fixture.Services, Token);

        var ticket = await TicketWriter.CreateAsync(fixture.Services, TicketWriter.Draft(reference), Token);

        ticket.Number.ShouldBe(TicketNumber.Format(TicketNumber.FirstValue));
    }

    private static string[] Expected(int from, int to) =>
        [.. Enumerable.Range(from, to - from + 1).Select(value => TicketNumber.Format(value))];
}
