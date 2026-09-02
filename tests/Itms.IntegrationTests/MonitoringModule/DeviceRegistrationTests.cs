using System.Net;
using Itms.IntegrationTests.Api;
using Itms.IntegrationTests.AssetsModule;
using Itms.IntegrationTests.Identity;
using Itms.Modules.Monitoring.Domain;

namespace Itms.IntegrationTests.MonitoringModule;

/// <summary>
/// Invariant 6 over the wire: a monitored device is always an asset, and monitoring cannot
/// create device records of its own.
/// </summary>
/// <remarks>
/// <b>This is WP-3.1's done-criterion and it can only be asserted here.</b> The rule spans
/// two modules — Monitoring may reference neither <c>Modules.Assets</c> nor the assets
/// schema, so the only thing that can prove it holds is a request against the real
/// <c>IAssetLookup</c> and the real unique index.
/// </remarks>
[Collection(IdentityTestGroup.Name)]
public sealed class DeviceRegistrationTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_administrator_registers_a_device_over_an_asset_and_reads_it_back()
    {
        using var admin = await SignedInAsync("admin");
        var asset = await AnAssetAsync(admin, "SRV-3100");

        var device = await DevicesClient.RegisterAsync(admin, asset.Id, "sw-core-01", Token);

        device.AssetId.ShouldBe(asset.Id);
        device.AssetTag.ShouldBe("SRV-3100");
        device.Hostname.ShouldBe("sw-core-01");

        var fetched = await DevicesClient.GetAsync(admin, device.Id, Token);
        fetched.Id.ShouldBe(device.Id);
    }

    /// <summary>
    /// The invariant. There is no branch anywhere in this module in which an unresolved
    /// asset produces a device: <c>IAssetLookup</c> answers null and the handler stops.
    /// </summary>
    [Fact]
    public async Task Monitoring_cannot_register_a_device_for_an_asset_that_does_not_exist()
    {
        using var admin = await SignedInAsync("admin");

        var response = await DevicesClient.PostDeviceAsync(
            admin,
            new { assetId = Guid.CreateVersion7(), hostname = "sw-ghost-01" },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("monitoring.asset_not_found");
        problem.Errors.ShouldNotBeNull().ShouldContainKey("assetId");

        // And nothing was written: the register is still empty.
        var devices = await ApiClient.ListAsync<DeviceDto>(admin, DevicesClient.Devices, Token);
        devices.Total.ShouldBe(0);
    }

    /// <summary>
    /// One asset, at most one device. A second row would give one machine two monitoring
    /// states and two of everything downstream.
    /// </summary>
    [Fact]
    public async Task An_asset_can_only_be_monitored_once()
    {
        using var admin = await SignedInAsync("admin");
        var asset = await AnAssetAsync(admin, "SRV-3101");

        await DevicesClient.RegisterAsync(admin, asset.Id, "sw-core-01", Token);

        var second = await DevicesClient.PostDeviceAsync(
            admin,
            new { assetId = asset.Id, hostname = "sw-core-01-again" },
            Token);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await ApiClient.ReadAsync<ProblemDto>(second, Token);
        problem.Code.ShouldBe("monitoring.device_already_registered");
        problem.Detail.ShouldNotBeNull().ShouldContain("SRV-3101");
    }

    /// <summary>A device the poller could never reach is refused with both fields named.</summary>
    [Fact]
    public async Task A_device_with_neither_a_hostname_nor_an_address_is_refused()
    {
        using var admin = await SignedInAsync("admin");
        var asset = await AnAssetAsync(admin, "SRV-3102");

        var response = await DevicesClient.PostDeviceAsync(admin, new { assetId = asset.Id }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Errors.ShouldNotBeNull().ShouldContainKey("hostname");
    }

    [Fact]
    public async Task A_malformed_address_is_refused_with_the_message_on_that_field()
    {
        using var admin = await SignedInAsync("admin");
        var asset = await AnAssetAsync(admin, "SRV-3103");

        var response = await DevicesClient.PostDeviceAsync(
            admin,
            new { assetId = asset.Id, ipAddress = "10.4.0.999" },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ApiClient.ReadAsync<ProblemDto>(response, Token);
        problem.Errors.ShouldNotBeNull().ShouldContainKey("ipAddress");
    }

    /// <summary>Registering a device is asking for it to be watched, so it arrives watched.</summary>
    [Fact]
    public async Task A_new_device_arrives_monitored_with_the_architecture_defaults()
    {
        using var admin = await SignedInAsync("admin");
        var asset = await AnAssetAsync(admin, "SRV-3104");

        var device = await DevicesClient.RegisterAsync(admin, asset.Id, "sw-core-01", Token);

        device.MonitoringEnabled.ShouldBeTrue();
        device.PollIntervalSeconds.ShouldBe(MonitoredDevice.DefaultPollIntervalSeconds);
        device.FailureThreshold.ShouldBe(MonitoredDevice.DefaultFailureThreshold);
        device.SnmpEnabled.ShouldBeFalse();
        device.SnmpPort.ShouldBe(SnmpSettings.DefaultPort);
        device.SnmpCredentialSet.ShouldBeFalse();
    }

    /// <summary>The 201's <c>Location</c> header has to point at a route that serves.</summary>
    [Fact]
    public async Task The_created_location_header_resolves_and_carries_an_etag()
    {
        using var admin = await SignedInAsync("admin");
        var asset = await AnAssetAsync(admin, "SRV-3105");

        var response = await DevicesClient.PostDeviceAsync(
            admin,
            new { assetId = asset.Id, hostname = "sw-core-01" },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.ETag.ShouldNotBeNull();

        var location = response.Headers.Location.ShouldNotBeNull();
        var followed = await admin.GetAsync(location, Token);
        followed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// The address is stored as <c>inet</c> and read back canonically, so the API answers
    /// one spelling rather than echoing what was typed.
    /// </summary>
    [Fact]
    public async Task An_address_is_stored_and_answered_canonically()
    {
        using var admin = await SignedInAsync("admin");
        var asset = await AnAssetAsync(admin, "SRV-3106");

        var response = await DevicesClient.PostDeviceAsync(
            admin,
            new { assetId = asset.Id, ipAddress = "2001:0db8:0000:0000:0000:0000:0000:0001" },
            Token);

        response.EnsureSuccessStatusCode();
        var device = await ApiClient.ReadAsync<DeviceDto>(response, Token);

        device.IpAddress.ShouldBe("2001:db8::1");
        (await DevicesClient.GetAsync(admin, device.Id, Token)).IpAddress.ShouldBe("2001:db8::1");
    }

    private static async Task<AssetDto> AnAssetAsync(HttpClient admin, string assetTag)
    {
        var typeId = await AssetsClient.AnyTypeIdAsync(admin, Token);
        return await AssetsClient.CreateAssetAsync(admin, assetTag, typeId, Token);
    }

    private async Task<HttpClient> SignedInAsync(string userName) =>
        await AuthClient.SignedInAsync(fixture, userName, Token);
}
