using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.AssetsModule;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.MonitoringModule;

/// <summary>
/// The device surface over the wire: who may reach it, what the list answers, and what an
/// edit may and may not move.
/// </summary>
/// <remarks>
/// <b>The role split is tighter than the asset endpoints'.</b> A technician chasing an
/// outage reads the register without needing an administrator, but which equipment is
/// watched and with what credential is configuration — so every write is Admin. That is a
/// decision rather than an oversight, and it is asserted here so it cannot drift.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class DeviceEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_technician_reads_devices_but_cannot_register_one()
    {
        using var admin = await SignedInAsync("admin");
        using var tech = await SignedInAsync("tech");
        var device = await ADeviceAsync(admin, "SRV-3300", "sw-core-01");

        (await DevicesClient.GetAsync(tech, device.Id, Token)).Id.ShouldBe(device.Id);
        (await ApiClient.ListAsync<DeviceDto>(tech, DevicesClient.Devices, Token)).Total.ShouldBe(1);

        var asset = await AnAssetAsync(admin, "SRV-3301");
        var refused = await DevicesClient.PostDeviceAsync(
            tech,
            new { assetId = asset.Id, hostname = "sw-edge-01" },
            Token);

        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>An end user has no business enumerating the monitored estate.</summary>
    [Fact]
    public async Task An_end_user_cannot_read_devices()
    {
        using var admin = await SignedInAsync("admin");
        using var user = await SignedInAsync("user");
        var device = await ADeviceAsync(admin, "SRV-3302", "sw-core-01");

        var list = await user.GetAsync(new Uri(DevicesClient.Devices, UriKind.Relative), Token);
        var detail = await user.GetAsync(
            new Uri($"{DevicesClient.Devices}/{device.Id}", UriKind.Relative),
            Token);

        list.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        detail.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_anonymous_caller_gets_a_401_rather_than_markup()
    {
        using var anonymous = fixture.CreateClient();

        var response = await anonymous.GetAsync(new Uri(DevicesClient.Devices, UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_list_filters_by_the_monitoring_switch_and_searches_tag_and_hostname()
    {
        using var admin = await SignedInAsync("admin");
        var watched = await ADeviceAsync(admin, "SRV-3303", "sw-core-01");
        var unwatched = await ADeviceAsync(admin, "PRN-3304", "printer-hr-02");

        await DevicesClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"{DevicesClient.Devices}/{unwatched.Id}/disable",
            body: null,
            Token);

        var enabled = await ApiClient.ListAsync<DeviceDto>(
            admin,
            $"{DevicesClient.Devices}?monitoringEnabled=true",
            Token);
        enabled.Items.Select(device => device.Id).ShouldBe([watched.Id]);

        var disabled = await ApiClient.ListAsync<DeviceDto>(
            admin,
            $"{DevicesClient.Devices}?monitoringEnabled=false",
            Token);
        disabled.Items.Select(device => device.Id).ShouldBe([unwatched.Id]);

        // Case-insensitive, and over both columns.
        (await ApiClient.ListAsync<DeviceDto>(admin, $"{DevicesClient.Devices}?search=PRINTER", Token))
            .Items.Select(device => device.Id).ShouldBe([unwatched.Id]);
        (await ApiClient.ListAsync<DeviceDto>(admin, $"{DevicesClient.Devices}?search=srv-3303", Token))
            .Items.Select(device => device.Id).ShouldBe([watched.Id]);
    }

    /// <summary>
    /// The escaping WP-1.12 hoisted into the shared kernel: an unescaped <c>%</c> typed
    /// into the box would otherwise match the whole table.
    /// </summary>
    [Fact]
    public async Task A_wildcard_typed_into_the_search_matches_nothing()
    {
        using var admin = await SignedInAsync("admin");
        await ADeviceAsync(admin, "SRV-3305", "sw-core-01");

        var page = await ApiClient.ListAsync<DeviceDto>(admin, $"{DevicesClient.Devices}?search=%25", Token);

        page.Total.ShouldBe(0);
    }

    [Fact]
    public async Task The_list_defaults_to_asset_tag_ascending()
    {
        using var admin = await SignedInAsync("admin");
        await ADeviceAsync(admin, "SRV-3307", "sw-c");
        await ADeviceAsync(admin, "PRN-3306", "sw-a");

        var page = await ApiClient.ListAsync<DeviceDto>(admin, DevicesClient.Devices, Token);

        page.Items.Select(device => device.AssetTag).ShouldBe(["PRN-3306", "SRV-3307"]);
    }

    [Fact]
    public async Task An_edit_replaces_the_addressing_and_polling_settings()
    {
        using var admin = await SignedInAsync("admin");
        var device = await ADeviceAsync(admin, "SRV-3308", "sw-core-01");

        var response = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{DevicesClient.Devices}/{device.Id}",
            new
            {
                hostname = "sw-core-99",
                ipAddress = "10.4.0.9",
                pollIntervalSeconds = 300,
                failureThreshold = 5,
                snmpEnabled = true,
                snmpPort = 1610,
            },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ApiClient.ReadAsync<DeviceDto>(response, Token);

        updated.Hostname.ShouldBe("sw-core-99");
        updated.IpAddress.ShouldBe("10.4.0.9");
        updated.PollIntervalSeconds.ShouldBe(300);
        updated.FailureThreshold.ShouldBe(5);
        updated.SnmpEnabled.ShouldBeTrue();
        updated.SnmpPort.ShouldBe(1610);
    }

    /// <summary>
    /// Invariant 6's other structural half: the edit shape has no field for the asset, so
    /// a body naming one is simply ignored rather than obeyed.
    /// </summary>
    [Fact]
    public async Task An_edit_cannot_repoint_the_device_at_another_asset()
    {
        using var admin = await SignedInAsync("admin");
        var device = await ADeviceAsync(admin, "SRV-3309", "sw-core-01");
        var other = await AnAssetAsync(admin, "SRV-3310");

        var response = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{DevicesClient.Devices}/{device.Id}",
            new { hostname = "sw-core-01", assetId = other.Id },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await DevicesClient.GetAsync(admin, device.Id, Token)).AssetId.ShouldBe(device.AssetId);
    }

    /// <summary>
    /// An unchanged form must not move <c>xmin</c>, or it would refuse every other reader's
    /// precondition for a change that never happened.
    /// </summary>
    [Fact]
    public async Task An_edit_that_moves_nothing_leaves_the_etag_alone()
    {
        using var admin = await SignedInAsync("admin");
        var device = await ADeviceAsync(admin, "SRV-3311", "sw-core-01");
        var before = await DevicesClient.ETagAsync(admin, device.Id, Token);

        var response = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{DevicesClient.Devices}/{device.Id}",
            new { hostname = "sw-core-01" },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await DevicesClient.ETagAsync(admin, device.Id, Token)).ShouldBe(before);
    }

    [Fact]
    public async Task A_stale_precondition_is_refused_with_412_and_a_fresh_one_is_honoured()
    {
        using var admin = await SignedInAsync("admin");
        var device = await ADeviceAsync(admin, "SRV-3312", "sw-core-01");
        var stale = await DevicesClient.ETagAsync(admin, device.Id, Token);

        // Move the device so the tag somebody else is holding goes out of date.
        await DevicesClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{DevicesClient.Devices}/{device.Id}",
            new { hostname = "sw-core-02" },
            Token);

        var refused = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{DevicesClient.Devices}/{device.Id}",
            new { hostname = "sw-core-03" },
            Token,
            stale);

        refused.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        (await ApiClient.ReadAsync<ProblemDto>(refused, Token)).Code.ShouldBe("monitoring.device_conflict");

        var fresh = await DevicesClient.ETagAsync(admin, device.Id, Token);
        var accepted = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{DevicesClient.Devices}/{device.Id}",
            new { hostname = "sw-core-03" },
            Token,
            fresh);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// RFC 9110 §13.1.1 makes <c>If-Match</c> a strong comparison, so a weak tag can never
    /// satisfy it — the assertion that caught the first implementation of the shared helper
    /// at WP-1.5.
    /// </summary>
    [Fact]
    public async Task A_weak_tag_does_not_satisfy_the_precondition()
    {
        using var admin = await SignedInAsync("admin");
        var device = await ADeviceAsync(admin, "SRV-3313", "sw-core-01");
        var tag = await DevicesClient.ETagAsync(admin, device.Id, Token);

        var response = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{DevicesClient.Devices}/{device.Id}",
            new { hostname = "sw-core-02" },
            Token,
            $"W/{tag}");

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task The_monitoring_switch_moves_the_device_and_is_idempotent()
    {
        using var admin = await SignedInAsync("admin");
        var device = await ADeviceAsync(admin, "SRV-3314", "sw-core-01");

        var disabled = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"{DevicesClient.Devices}/{device.Id}/disable",
            body: null,
            Token);

        disabled.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await DevicesClient.GetAsync(admin, device.Id, Token)).MonitoringEnabled.ShouldBeFalse();

        // Asking again for a state it already holds is accepted and moves nothing.
        var tag = await DevicesClient.ETagAsync(admin, device.Id, Token);
        var again = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"{DevicesClient.Devices}/{device.Id}/disable",
            body: null,
            Token);

        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await DevicesClient.ETagAsync(admin, device.Id, Token)).ShouldBe(tag);

        var enabled = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"{DevicesClient.Devices}/{device.Id}/enable",
            body: null,
            Token);

        enabled.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await DevicesClient.GetAsync(admin, device.Id, Token)).MonitoringEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task A_device_that_does_not_exist_is_a_404_on_every_route()
    {
        using var admin = await SignedInAsync("admin");
        var missing = Guid.CreateVersion7();

        var read = await admin.GetAsync(new Uri($"{DevicesClient.Devices}/{missing}", UriKind.Relative), Token);
        var edit = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"{DevicesClient.Devices}/{missing}",
            new { hostname = "sw-core-01" },
            Token);
        var switched = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Post,
            $"{DevicesClient.Devices}/{missing}/disable",
            body: null,
            Token);
        var credential = await DevicesClient.SendAsync(
            admin,
            HttpMethod.Delete,
            $"{DevicesClient.Devices}/{missing}/snmp-credential",
            body: null,
            Token);

        read.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        edit.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        switched.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        credential.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Cookie auth plus a state-changing verb is exactly the shape CSRF exploits, and
    /// CONVENTIONS.md's security floor requires the check on every one of them. Asserted
    /// once per verb shape rather than once per route.
    /// </summary>
    [Fact]
    public async Task A_write_without_an_antiforgery_token_is_refused()
    {
        using var admin = await SignedInAsync("admin");
        var device = await ADeviceAsync(admin, "SRV-3315", "sw-core-01");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{DevicesClient.Devices}/{device.Id}/disable");

        var response = await admin.SendAsync(request, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await DevicesClient.GetAsync(admin, device.Id, Token)).MonitoringEnabled.ShouldBeTrue();
    }

    private static async Task<DeviceDto> ADeviceAsync(HttpClient admin, string assetTag, string hostname)
    {
        var asset = await AnAssetAsync(admin, assetTag);
        return await DevicesClient.RegisterAsync(admin, asset.Id, hostname, Token);
    }

    private static async Task<AssetDto> AnAssetAsync(HttpClient admin, string assetTag)
    {
        var typeId = await AssetsClient.AnyTypeIdAsync(admin, Token);
        return await AssetsClient.CreateAssetAsync(admin, assetTag, typeId, Token);
    }

    private async Task<HttpClient> SignedInAsync(string userName) =>
        await AuthClient.SignedInAsync(fixture, userName, Token);
}
