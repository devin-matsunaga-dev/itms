using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.AssetsModule;

/// <summary>
/// The ETag and <c>If-Match</c> surface ARCHITECTURE.md §6 asks for on assets as well as
/// tickets. WP-2.1 mapped the <c>xmin</c> token and had nothing to race against; this is
/// where it starts refusing stale writes.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class AssetPreconditionTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Every read and every write answers with a tag, so a client always leaves an exchange
    /// holding a precondition it can state on the next one.
    /// </summary>
    [Fact]
    public async Task Every_asset_exchange_answers_with_an_entity_tag()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        var create = await AssetsClient.PostAssetAsync(
            tech,
            new { assetTag = "LAP-0500", assetTypeId = typeId },
            Token);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        create.Headers.ETag.ShouldNotBeNull();

        var created = await ApiClient.ReadAsync<AssetDto>(create, Token);

        var (_, readTag) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);
        readTag.ShouldNotBeNullOrWhiteSpace();

        var write = await AssetsClient.SendForRepairAsync(tech, created.Id, Token);
        write.Headers.ETag.ShouldNotBeNull();
    }

    /// <summary>The tag a write answers with is the one the next read confirms.</summary>
    [Fact]
    public async Task The_tag_a_write_answers_with_is_the_assets_new_version()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0501", typeId, Token);

        var write = await AssetsClient.SendForRepairAsync(tech, created.Id, Token);
        write.EnsureSuccessStatusCode();
        var afterWrite = write.Headers.ETag!.ToString();

        var (_, afterRead) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);

        afterRead.ShouldBe(afterWrite);
    }

    [Fact]
    public async Task A_current_precondition_is_honoured()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0502", typeId, Token);

        var (_, tag) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);

        var response = await AssetsClient.SendForRepairAsync(tech, created.Id, Token, ifMatch: tag);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// The point of stating a precondition: told before the write, not after. A second
    /// technician holding the tag from before the first one's change is refused.
    /// </summary>
    [Fact]
    public async Task A_stale_precondition_is_refused_with_412_and_changes_nothing()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0503", typeId, Token);

        var (_, stale) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);

        (await AssetsClient.SendForRepairAsync(tech, created.Id, Token)).EnsureSuccessStatusCode();

        var response = await AssetsClient.RetireAsync(tech, created.Id, Token, ifMatch: stale);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("assets.asset_conflict");

        // Refused before anything was attempted: no transition, and no timeline entry for one.
        var (unchanged, _) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);
        unchanged.AssetStatusCode.ShouldBe("repair");
        (await AssetsClient.HistoryAsync(tech, created.Id, Token)).Total.ShouldBe(1);
    }

    /// <summary>
    /// RFC 9110 §13.1.1: <c>*</c> matches any existing representation, and the row was
    /// found, so it matches.
    /// </summary>
    [Fact]
    public async Task A_star_precondition_matches_any_existing_asset()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0504", typeId, Token);

        var response = await AssetsClient.SendForRepairAsync(tech, created.Id, Token, ifMatch: "*");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// A weak tag can never satisfy <c>If-Match</c>, which uses the strong comparison
    /// function. Skipping it rather than parsing its value is the difference between
    /// refusing a stale write and waving it through.
    /// </summary>
    [Fact]
    public async Task A_weak_tag_never_satisfies_a_precondition()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0505", typeId, Token);

        var (_, tag) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);

        var response = await AssetsClient.SendForRepairAsync(tech, created.Id, Token, ifMatch: $"W/{tag}");

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
    }

    /// <summary>
    /// A header that is present but unparseable did state a precondition, and it is one
    /// nothing can satisfy — a failed precondition rather than a bad request.
    /// </summary>
    [Fact]
    public async Task A_malformed_precondition_is_a_412_not_a_400()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0506", typeId, Token);

        var response = await AssetsClient.SendForRepairAsync(tech, created.Id, Token, ifMatch: "not-a-tag");

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task An_assignment_honours_a_precondition_too()
    {
        using var tech = await SignedInAsync("tech");
        var alice = await UserIdAsync("user");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0507", typeId, Token);

        var (_, stale) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);
        (await AssetsClient.SendForRepairAsync(tech, created.Id, Token)).EnsureSuccessStatusCode();

        var response = await AssetsClient.AssignAsync(tech, created.Id, alice, Token, ifMatch: stale);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
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
