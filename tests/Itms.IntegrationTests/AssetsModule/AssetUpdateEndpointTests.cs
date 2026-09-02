using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.AssetsModule;

/// <summary>
/// <c>PUT /api/v1/assets/{id}</c> — the correction path WP-2.6b added, approved at the
/// WP-2.6 scope gate.
/// </summary>
/// <remarks>
/// What is being held here is the boundary between a correction and a lifecycle move. The
/// edit replaces the descriptive half of an asset and must be unable to reach the tag
/// (invariant 4), the status, or the holder — the last two because moving either owes a
/// history entry and a domain event, and this route writes neither.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class AssetUpdateEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_edit_replaces_the_descriptive_fields_and_answers_with_the_asset()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0600", typeId, Token);

        var response = await AssetsClient.PutAssetAsync(
            tech,
            created.Id,
            Edit(typeId) with
            {
                Name = "Reception desktop",
                SerialNumber = "CND1234XYZ",
                Barcode = "BC-8891",
                Manufacturer = "HP",
                Model = "EliteDesk 800",
                PurchaseDate = "2026-03-14",
                WarrantyExpiresAt = "2029-03-13",
                Vendor = "Insight",
                Cost = 1249.99m,
                Notes = "Second monitor issued with it.",
            },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var edited = await ApiClient.ReadAsync<AssetDto>(response, Token);
        edited.Name.ShouldBe("Reception desktop");
        edited.SerialNumber.ShouldBe("CND1234XYZ");
        edited.Barcode.ShouldBe("BC-8891");
        edited.Manufacturer.ShouldBe("HP");
        edited.Model.ShouldBe("EliteDesk 800");
        edited.PurchaseDate.ShouldBe(new DateOnly(2026, 3, 14));
        edited.WarrantyExpiresAt.ShouldBe(new DateOnly(2029, 3, 13));
        edited.Vendor.ShouldBe("Insight");
        edited.Cost.ShouldBe(1249.99m);
        edited.Notes.ShouldBe("Second monitor issued with it.");

        // And it persisted, rather than only being echoed back.
        var (reread, _) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);
        reread.Name.ShouldBe("Reception desktop");
        reread.SerialNumber.ShouldBe("CND1234XYZ");
    }

    /// <summary>
    /// A PUT is a full replacement, so a field left out of the body is cleared. The edit
    /// form posts every field, which makes "the operator emptied the box" and "the client
    /// omitted it" the same request — and they must mean the same thing.
    /// </summary>
    [Fact]
    public async Task A_field_left_out_of_the_body_is_cleared()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateDetailedAsync(
            tech,
            new { assetTag = "LAP-0601", assetTypeId = typeId, vendor = "Insight", notes = "Bought in bulk." },
            Token);
        created.Vendor.ShouldBe("Insight");

        var response = await AssetsClient.PutAssetAsync(tech, created.Id, Edit(typeId), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var edited = await ApiClient.ReadAsync<AssetDto>(response, Token);
        edited.Vendor.ShouldBeNull();
        edited.Notes.ShouldBeNull();
    }

    /// <summary>
    /// Invariant 4 over the wire. <c>UpdateAssetRequest</c> has no tag field, so one sent
    /// anyway is not bound and cannot reach the entity.
    /// </summary>
    [Fact]
    public async Task An_asset_tag_sent_in_an_edit_is_ignored()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0602", typeId, Token);

        var response = await AssetsClient.PutAssetAsync(
            tech,
            created.Id,
            new { assetTag = "LAP-9999", assetTypeId = typeId },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadAsync<AssetDto>(response, Token)).AssetTag.ShouldBe("LAP-0602");
    }

    /// <summary>
    /// The status and the holder move through the lifecycle routes, which write a history
    /// entry (invariant 5) and publish an event. An edit that could move either would route
    /// round both.
    /// </summary>
    [Fact]
    public async Task An_edit_moves_neither_the_status_nor_the_holder_and_writes_no_history()
    {
        using var tech = await SignedInAsync("tech");
        var alice = await UserIdAsync("user");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0603", typeId, Token);

        (await AssetsClient.AssignAsync(tech, created.Id, alice, Token)).EnsureSuccessStatusCode();
        var before = await AssetsClient.HistoryAsync(tech, created.Id, Token);

        var retired = await AssetsClient.StatusByCodeAsync(tech, "retired", Token);
        var response = await AssetsClient.PutAssetAsync(
            tech,
            created.Id,
            new
            {
                assetTypeId = typeId,
                name = "Corrected",
                assetStatusId = retired.Id,
                assignedToUserId = (Guid?)null,
            },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var edited = await ApiClient.ReadAsync<AssetDto>(response, Token);
        edited.Name.ShouldBe("Corrected");
        edited.AssetStatusCode.ShouldBe("deployed");
        edited.AssignedToUserId.ShouldBe(alice);

        // No timeline entry: an edit is not one of invariant 5's five moves.
        (await AssetsClient.HistoryAsync(tech, created.Id, Token)).Total.ShouldBe(before.Total);
    }

    /// <summary>
    /// The 409 WP-2.1's rule asks for, from the other side: the serial is unique per
    /// manufacturer, and an edit must not be a way round the index the create respects.
    /// </summary>
    [Fact]
    public async Task An_edit_onto_another_assets_serial_is_refused_with_409()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        await AssetsClient.CreateDetailedAsync(
            tech,
            new { assetTag = "LAP-0604", assetTypeId = typeId, manufacturer = "HP", serialNumber = "CND1234XYZ" },
            Token);
        var second = await AssetsClient.CreateAssetAsync(tech, "LAP-0605", typeId, Token);

        var response = await AssetsClient.PutAssetAsync(
            tech,
            second.Id,
            Edit(typeId) with { Manufacturer = "hp", SerialNumber = "cnd1234xyz" },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code
            .ShouldBe("assets.duplicate_serial_number");
    }

    /// <summary>
    /// The asset being edited is excluded from its own uniqueness check, or every edit that
    /// left the serial alone would collide with itself.
    /// </summary>
    [Fact]
    public async Task An_edit_that_keeps_its_own_serial_is_not_a_collision()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateDetailedAsync(
            tech,
            new { assetTag = "LAP-0606", assetTypeId = typeId, manufacturer = "HP", serialNumber = "CND1234XYZ" },
            Token);

        var response = await AssetsClient.PutAssetAsync(
            tech,
            created.Id,
            Edit(typeId) with { Manufacturer = "HP", SerialNumber = "CND1234XYZ", Name = "Renamed" },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadAsync<AssetDto>(response, Token)).Name.ShouldBe("Renamed");
    }

    /// <summary>
    /// Two technicians on the same asset screen. The second is told before the write rather
    /// than after it.
    /// </summary>
    [Fact]
    public async Task A_stale_precondition_is_refused_with_412_and_changes_nothing()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0607", typeId, Token);

        var (_, stale) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);
        (await AssetsClient.PutAssetAsync(tech, created.Id, Edit(typeId) with { Name = "First" }, Token))
            .EnsureSuccessStatusCode();

        var response = await AssetsClient.PutAssetAsync(
            tech,
            created.Id,
            Edit(typeId) with { Name = "Second" },
            Token,
            ifMatch: stale);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("assets.asset_conflict");

        var (unchanged, _) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);
        unchanged.Name.ShouldBe("First");
    }

    [Fact]
    public async Task A_current_precondition_is_honoured_and_the_response_carries_the_new_tag()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0608", typeId, Token);

        var (_, tag) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);

        var response = await AssetsClient.PutAssetAsync(
            tech,
            created.Id,
            Edit(typeId) with { Name = "Reception desktop" },
            Token,
            ifMatch: tag);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();

        var (_, afterRead) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);
        afterRead.ShouldBe(response.Headers.ETag!.ToString());
    }

    /// <summary>
    /// A form re-submitted unchanged must not bump <c>xmin</c>, or it would silently refuse
    /// every other reader's precondition for a change that never happened.
    /// </summary>
    [Fact]
    public async Task An_edit_that_moves_nothing_leaves_the_entity_tag_alone()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateDetailedAsync(
            tech,
            new { assetTag = "LAP-0609", assetTypeId = typeId, name = "Reception desktop" },
            Token);

        var (_, before) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);

        var response = await AssetsClient.PutAssetAsync(
            tech,
            created.Id,
            Edit(typeId) with { Name = "Reception desktop" },
            Token,
            ifMatch: before);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (_, after) = await AssetsClient.GetAssetAsync(tech, created.Id, Token);
        after.ShouldBe(before);
    }

    [Fact]
    public async Task An_unknown_asset_is_a_404()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);

        var response = await AssetsClient.PutAssetAsync(tech, Guid.CreateVersion7(), Edit(typeId), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unknown_asset_type_is_a_404()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0610", typeId, Token);

        var response = await AssetsClient.PutAssetAsync(
            tech,
            created.Id,
            Edit(Guid.CreateVersion7()),
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("assets.asset_type_not_found");
    }

    /// <summary>
    /// Retiring an asset type must not freeze the equipment already classified as one, so
    /// keeping the type is allowed and moving into it is not.
    /// </summary>
    [Fact]
    public async Task A_retired_type_may_be_kept_but_not_moved_into()
    {
        using var admin = await SignedInAsync("admin");
        var doomed = await AssetsClient.CreateTypeAsync(admin, "Fax machines", 900, Token);
        var created = await AssetsClient.CreateAssetAsync(admin, "LAP-0611", doomed.Id, Token);
        var other = await AssetsClient.CreateTypeAsync(admin, "Plotters", 901, Token);
        var stillHere = await AssetsClient.AnyTypeIdAsync(admin, Token);

        (await ApiClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"{AssetsClient.Types}/{doomed.Id}/deactivate",
            null,
            Token)).EnsureSuccessStatusCode();
        (await ApiClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"{AssetsClient.Types}/{other.Id}/deactivate",
            null,
            Token)).EnsureSuccessStatusCode();

        // The asset's own retired type is still acceptable: the edit is not a move.
        var kept = await AssetsClient.PutAssetAsync(
            admin,
            created.Id,
            Edit(doomed.Id) with { Name = "Corrected" },
            Token);
        kept.StatusCode.ShouldBe(HttpStatusCode.OK);

        // A different retired type is a move into one, and is refused.
        var moved = await AssetsClient.PutAssetAsync(admin, created.Id, Edit(other.Id), Token);
        moved.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(moved, Token)).Code.ShouldBe("assets.asset_type_retired");

        // And an active one is fine.
        (await AssetsClient.PutAssetAsync(admin, created.Id, Edit(stillHere), Token))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// An edit reads both cached display strings fresh through Directory's contracts, so
    /// saving an asset's form brings its department name and location path back into
    /// agreement. That is a side effect of the edit rather than the refresh consumer
    /// STATUS.md still owes.
    /// </summary>
    [Fact]
    public async Task An_edit_caches_the_department_name_and_location_path_afresh()
    {
        using var admin = await SignedInAsync("admin");
        var typeId = await AssetsClient.AnyTypeIdAsync(admin, Token);
        var created = await AssetsClient.CreateAssetAsync(admin, "LAP-0612", typeId, Token);

        var department = await DirectoryModule.DirectoryClient.CreateDepartmentAsync(admin, "Water Division", "WATER", Token);
        // A root is an Organization and the tree does not skip levels, so the chain is built
        // down from one — under names the development seeder does not already use.
        var org = await DirectoryModule.DirectoryClient.CreateLocationAsync(
            admin, "Sadog Tasi Group", "Organization", null, Token);
        var site = await DirectoryModule.DirectoryClient.CreateLocationAsync(
            admin, "Sadog Tasi Yard", "Site", org.Id, Token);
        var room = await DirectoryModule.DirectoryClient.CreateLocationAsync(
            admin, "Store room", "Room", site.Id, Token);

        var response = await AssetsClient.PutAssetAsync(
            admin,
            created.Id,
            Edit(typeId) with { DepartmentId = department.Id, LocationId = room.Id },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var edited = await ApiClient.ReadAsync<AssetDto>(response, Token);
        edited.DepartmentId.ShouldBe(department.Id);
        edited.DepartmentName.ShouldBe("Water Division");
        edited.LocationId.ShouldBe(room.Id);
        edited.LocationPath.ShouldBe(room.Path);
    }

    [Fact]
    public async Task A_department_or_location_Directory_does_not_know_is_a_field_error()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0613", typeId, Token);

        var response = await AssetsClient.PutAssetAsync(
            tech,
            created.Id,
            Edit(typeId) with { DepartmentId = Guid.CreateVersion7() },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("assets.department_not_found");
        problem.Errors.ShouldNotBeNull().ShouldContainKey("departmentId");
    }

    /// <summary>
    /// The bounds are the same the create applies, and they come back keyed by field so an
    /// edit form can put them where they belong.
    /// </summary>
    [Fact]
    public async Task An_over_long_field_is_a_400_with_a_per_field_message()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0614", typeId, Token);

        var response = await AssetsClient.PutAssetAsync(
            tech,
            created.Id,
            Edit(typeId) with { Name = new string('A', 200) },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Errors
            .ShouldNotBeNull()
            .ShouldContainKey("name");
    }

    [Fact]
    public async Task A_negative_cost_is_refused()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0615", typeId, Token);

        var response = await AssetsClient.PutAssetAsync(
            tech,
            created.Id,
            Edit(typeId) with { Cost = -1m },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Errors
            .ShouldNotBeNull()
            .ShouldContainKey("cost");
    }

    /// <summary>
    /// Every asset route is Technician-or-Admin (SPEC.md §14). The register is not an end
    /// user's to correct.
    /// </summary>
    [Fact]
    public async Task An_end_user_cannot_edit_an_asset()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0616", typeId, Token);

        using var user = await SignedInAsync("user");
        var response = await AssetsClient.PutAssetAsync(user, created.Id, Edit(typeId), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Cookie auth plus a state-changing verb is the CSRF shape; CONVENTIONS.md's security
    /// floor requires the check on every one of them, and a new write path is exactly where
    /// it goes missing.
    /// </summary>
    [Fact]
    public async Task An_edit_without_an_antiforgery_token_is_refused()
    {
        using var tech = await SignedInAsync("tech");
        var typeId = await AssetsClient.AnyTypeIdAsync(tech, Token);
        var created = await AssetsClient.CreateAssetAsync(tech, "LAP-0617", typeId, Token);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"{AssetsClient.Assets}/{created.Id}")
        {
            Content = System.Net.Http.Json.JsonContent.Create(Edit(typeId)),
        };

        var response = await tech.SendAsync(request, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("auth.antiforgery_failed");
    }

    /// <summary>
    /// The body every test starts from: the type, and every other field cleared.
    /// </summary>
    /// <remarks>
    /// A record rather than an anonymous object so a test can say <c>with { Name = … }</c>
    /// and change one field of a full replacement without restating the other twelve —
    /// which is the shape of the thing being asserted. The names are PascalCase and the
    /// suite's <c>JsonSerializerDefaults.Web</c> camel-cases them on the way out, so what
    /// goes on the wire is what <c>UpdateAssetRequest</c> binds.
    /// </remarks>
    /// <param name="assetTypeId">The type to classify the asset as.</param>
    /// <returns>An edit that clears every optional field.</returns>
    private static EditBody Edit(Guid assetTypeId) => new(
        assetTypeId,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    private sealed record EditBody(
        Guid AssetTypeId,
        string? Name,
        string? SerialNumber,
        string? Barcode,
        string? Manufacturer,
        string? Model,
        Guid? DepartmentId,
        Guid? LocationId,
        string? PurchaseDate,
        string? WarrantyExpiresAt,
        string? Vendor,
        decimal? Cost,
        string? Notes);

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
