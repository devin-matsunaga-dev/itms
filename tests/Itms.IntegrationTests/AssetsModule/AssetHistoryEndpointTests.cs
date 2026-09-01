using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Features.AssetHistory;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itms.IntegrationTests.AssetsModule;

/// <summary>
/// The asset timeline: how it reads, how it orders, and — the assertion that matters most —
/// that it cannot outlive the change it describes.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class AssetHistoryEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// An asset that exists and has not moved answers with an empty page; one that does not
    /// exist answers 404. Reading the history alone could not tell those apart.
    /// </summary>
    [Fact]
    public async Task An_asset_that_has_not_moved_has_an_empty_timeline()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0400", typeId, Token);

        var history = await AssetsClient.HistoryAsync(tech, created.Id, Token);

        history.Total.ShouldBe(0);
        history.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_history_of_an_unknown_asset_is_a_404()
    {
        using var tech = await SignedInAsync("tech");

        var response = await tech.GetAsync(
            new Uri($"{AssetsClient.Assets}/{Guid.CreateVersion7()}/history", UriKind.Relative),
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("assets.asset_not_found");
    }

    [Fact]
    public async Task An_end_user_cannot_read_an_asset_timeline()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0401", typeId, Token);

        using var user = await SignedInAsync("user");
        var response = await user.GetAsync(
            new Uri($"{AssetsClient.Assets}/{created.Id}/history", UriKind.Relative),
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Newest first, and entries from one operation ordered by their ordinal within it. A
    /// timeline whose order flips between two reads of the same data is unusable.
    /// </summary>
    [Fact]
    public async Task The_timeline_reads_newest_first_and_is_stable()
    {
        using var tech = await SignedInAsync("tech");
        var alice = await UserIdAsync("user");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0402", typeId, Token);

        (await AssetsClient.AssignAsync(tech, created.Id, alice, Token, note: "issued")).EnsureSuccessStatusCode();
        (await AssetsClient.SendForRepairAsync(tech, created.Id, Token, note: "keyboard")).EnsureSuccessStatusCode();
        (await AssetsClient.ReturnToServiceAsync(tech, created.Id, Token, note: "fixed")).EnsureSuccessStatusCode();

        var first = await AssetsClient.HistoryAsync(tech, created.Id, Token);
        var second = await AssetsClient.HistoryAsync(tech, created.Id, Token);

        // Two entries for the issue, one each for the repair and the return.
        first.Total.ShouldBe(4);
        first.Items.Select(entry => entry.Id).ShouldBe(second.Items.Select(entry => entry.Id));

        for (var i = 1; i < first.Items.Count; i++)
        {
            var newer = first.Items[i - 1];
            var older = first.Items[i];

            (newer.OccurredAt > older.OccurredAt
                || (newer.OccurredAt == older.OccurredAt && newer.Sequence > older.Sequence))
                .ShouldBeTrue($"entry {i - 1} should sort above entry {i}");
        }

        first.Items[0].Note.ShouldBe("fixed");
    }

    [Fact]
    public async Task The_timeline_pages()
    {
        using var tech = await SignedInAsync("tech");
        var alice = await UserIdAsync("user");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0403", typeId, Token);

        (await AssetsClient.AssignAsync(tech, created.Id, alice, Token)).EnsureSuccessStatusCode();
        (await AssetsClient.SendForRepairAsync(tech, created.Id, Token)).EnsureSuccessStatusCode();

        var page = await ApiClient.ListAsync<AssetHistoryDto>(
            tech,
            $"{AssetsClient.Assets}/{created.Id}/history?page=2&pageSize=2",
            Token);

        page.Total.ShouldBe(3);
        page.Page.ShouldBe(2);
        page.Items.Count.ShouldBe(1);
    }

    /// <summary>
    /// Invariant 5's real content: the history entry is written in the same transaction as
    /// the change, so a rolled-back transaction leaves no line claiming it happened. Driven
    /// against the recorder the handlers actually use, which is why it is public.
    /// </summary>
    [Fact]
    public async Task A_rolled_back_transaction_leaves_no_orphan_entry()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0404", typeId, Token);

        await using var scope = fixture.Services.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IModuleDbSession>();
        var database = scope.ServiceProvider.GetRequiredService<AssetsDbContext>();
        var recorder = scope.ServiceProvider.GetRequiredService<AssetHistoryRecorder>();

        await Should.ThrowAsync<InvalidOperationException>(() => session.ExecuteInTransactionAsync(
            async token =>
            {
                await session.EnlistAsync(database, token);

                var asset = await database.Assets.FirstAsync(candidate => candidate.Id == created.Id, token);
                var statuses = await database.AssetStatuses.AsNoTracking().ToListAsync(token);

                var current = AssetStatusRef.Of(statuses.First(row => row.Id == asset.AssetStatusId));
                var deployed = AssetStatusRef.Of(
                    statuses.First(row => row.Code == AssetStatusCode.Deployed));

                var before = AssetSnapshot.Of(asset, current);
                var now = DateTimeOffset.UtcNow;

                asset.AssignTo(Guid.CreateVersion7(), "Somebody Else", current, deployed, now, null)
                    .IsSuccess.ShouldBeTrue();

                recorder.Record(asset, before, deployed, now, "rolled back");
                await database.SaveChangesAsync(token);

                throw new InvalidOperationException("Forcing a rollback.");
            },
            Token));

        (await AssetsClient.HistoryAsync(tech, created.Id, Token)).Total.ShouldBe(0);
    }

    private async Task<Guid> UserIdAsync(string userName)
    {
        using var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        return (await AuthClient.ReadUserAsync(response, Token)).Id;
    }

    private Task<HttpClient> SignedInAsync(string userName) =>
        AuthClient.SignedInAsync(fixture, userName, Token);
}
