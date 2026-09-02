using System.Net;
using Itms.Modules.Monitoring.Domain;

namespace Itms.UnitTests.MonitoringModule;

/// <summary>
/// The monitored-device entity's own rules: what it refuses, what it normalises, and what
/// it will not let a caller reach.
/// </summary>
public sealed class MonitoredDeviceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly Guid AnAsset = Guid.CreateVersion7();

    /// <summary>
    /// Invariant 6's structural half. A device cannot be built without an asset, and the
    /// only caller that can supply one is the handler that resolved it through
    /// <c>IAssetLookup</c>.
    /// </summary>
    [Fact]
    public void A_device_cannot_be_registered_without_an_asset()
    {
        var register = () => MonitoredDevice.Register(
            NewDeviceFor(Guid.Empty),
            Now,
            Actor);

        register.ShouldThrow<ArgumentException>();
    }

    /// <summary>A device the poller could never reach is refused rather than silently skipped.</summary>
    [Fact]
    public void A_device_needs_a_hostname_or_an_address()
    {
        var register = () => MonitoredDevice.Register(
            NewDeviceFor(AnAsset) with { Hostname = null, IpAddress = null },
            Now,
            Actor);

        register.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void A_hostname_alone_is_enough()
    {
        var device = MonitoredDevice.Register(
            NewDeviceFor(AnAsset) with { Hostname = "sw-core-01", IpAddress = null },
            Now,
            Actor);

        device.Hostname.ShouldBe("sw-core-01");
        device.IpAddress.ShouldBeNull();
    }

    [Fact]
    public void An_address_alone_is_enough()
    {
        var device = MonitoredDevice.Register(
            NewDeviceFor(AnAsset) with { Hostname = null, IpAddress = IPAddress.Parse("10.4.0.9") },
            Now,
            Actor);

        device.Hostname.ShouldBeNull();
        device.IpAddress.ShouldBe(IPAddress.Parse("10.4.0.9"));
    }

    /// <summary>
    /// Hostnames are case-insensitive (RFC 4343), so the normalised form is what the
    /// search and the ordering read.
    /// </summary>
    [Fact]
    public void A_hostname_is_trimmed_and_normalised_to_lower_case()
    {
        var device = MonitoredDevice.Register(
            NewDeviceFor(AnAsset) with { Hostname = "  SW-Core-01  " },
            Now,
            Actor);

        device.Hostname.ShouldBe("SW-Core-01");
        device.NormalizedHostname.ShouldBe("sw-core-01");
    }

    [Fact]
    public void A_new_device_carries_the_defaults_it_was_given()
    {
        var device = MonitoredDevice.Register(NewDeviceFor(AnAsset), Now, Actor);

        device.PollIntervalSeconds.ShouldBe(MonitoredDevice.DefaultPollIntervalSeconds);
        device.FailureThreshold.ShouldBe(MonitoredDevice.DefaultFailureThreshold);
        device.SnmpPort.ShouldBe(SnmpSettings.DefaultPort);
        device.CreatedAt.ShouldBe(Now);
        device.CreatedBy.ShouldBe(Actor);
        device.UpdatedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData(MonitoredDevice.MinPollIntervalSeconds - 1)]
    [InlineData(MonitoredDevice.MaxPollIntervalSeconds + 1)]
    public void A_poll_interval_outside_the_bounds_is_refused(int seconds)
    {
        var register = () => MonitoredDevice.Register(
            NewDeviceFor(AnAsset) with { PollIntervalSeconds = seconds },
            Now,
            Actor);

        register.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(MonitoredDevice.MinFailureThreshold - 1)]
    [InlineData(MonitoredDevice.MaxFailureThreshold + 1)]
    public void A_failure_threshold_outside_the_bounds_is_refused(int failures)
    {
        var register = () => MonitoredDevice.Register(
            NewDeviceFor(AnAsset) with { FailureThreshold = failures },
            Now,
            Actor);

        register.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65_536)]
    public void A_port_outside_the_bounds_is_refused(int port)
    {
        var register = () => MonitoredDevice.Register(
            NewDeviceFor(AnAsset) with { SnmpPort = port },
            Now,
            Actor);

        register.ShouldThrow<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// The same call <c>Asset.Update</c> makes: an unchanged form must not move
    /// <c>xmin</c>, or it would refuse every other reader's precondition for a change that
    /// never happened.
    /// </summary>
    [Fact]
    public void An_edit_that_moves_nothing_leaves_the_audit_columns_alone()
    {
        var device = MonitoredDevice.Register(NewDeviceFor(AnAsset), Now, Actor);
        var later = Now.AddHours(1);

        device.Update(DeviceSettings.Of(device), later, Guid.CreateVersion7());

        device.UpdatedAt.ShouldBe(Now);
        device.UpdatedBy.ShouldBe(Actor);
    }

    [Fact]
    public void An_edit_that_moves_something_stamps_the_audit_columns()
    {
        var device = MonitoredDevice.Register(NewDeviceFor(AnAsset), Now, Actor);
        var later = Now.AddHours(1);
        var editor = Guid.CreateVersion7();

        var applied = device.Update(
            DeviceSettings.Of(device) with { PollIntervalSeconds = 300 },
            later,
            editor);

        applied.PollIntervalSeconds.ShouldBe(300);
        device.PollIntervalSeconds.ShouldBe(300);
        device.UpdatedAt.ShouldBe(later);
        device.UpdatedBy.ShouldBe(editor);
    }

    /// <summary>
    /// The edit is a full replacement, so clearing the hostname is a real instruction — but
    /// it cannot leave the device with no way to be reached.
    /// </summary>
    [Fact]
    public void An_edit_cannot_leave_the_device_unreachable()
    {
        var device = MonitoredDevice.Register(NewDeviceFor(AnAsset), Now, Actor);

        var update = () => device.Update(
            DeviceSettings.Of(device) with { Hostname = null, IpAddress = null },
            Now,
            Actor);

        update.ShouldThrow<ArgumentException>();
    }

    /// <summary>
    /// The whole point of <c>DeviceSettings</c> having no field for it: an edit cannot
    /// clear a secret it was never given.
    /// </summary>
    [Fact]
    public void An_edit_cannot_reach_the_snmp_credential()
    {
        var device = MonitoredDevice.Register(
            NewDeviceFor(AnAsset) with { SnmpCommunity = "public-ro" },
            Now,
            Actor);

        device.Update(DeviceSettings.Of(device) with { Hostname = "renamed-host" }, Now, Actor);

        device.HasSnmpCredential.ShouldBeTrue();
        device.SnmpCommunity.ShouldBe("public-ro");
    }

    /// <summary>An edit cannot switch monitoring either — that is its own operation.</summary>
    [Fact]
    public void An_edit_cannot_reach_the_monitoring_switch()
    {
        var device = MonitoredDevice.Register(
            NewDeviceFor(AnAsset) with { MonitoringEnabled = true },
            Now,
            Actor);

        device.Update(DeviceSettings.Of(device) with { SnmpPort = 1610 }, Now, Actor);

        device.MonitoringEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Switching_monitoring_to_the_state_it_already_holds_moves_nothing()
    {
        var device = MonitoredDevice.Register(
            NewDeviceFor(AnAsset) with { MonitoringEnabled = true },
            Now,
            Actor);

        device.SetMonitoringEnabled(enabled: true, Now.AddHours(1), Guid.CreateVersion7()).ShouldBeFalse();
        device.UpdatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Switching_monitoring_off_moves_the_device()
    {
        var device = MonitoredDevice.Register(
            NewDeviceFor(AnAsset) with { MonitoringEnabled = true },
            Now,
            Actor);

        var later = Now.AddHours(1);

        device.SetMonitoringEnabled(enabled: false, later, Actor).ShouldBeTrue();
        device.MonitoringEnabled.ShouldBeFalse();
        device.UpdatedAt.ShouldBe(later);
    }

    /// <summary>
    /// Deliberately no short-circuit on "it is already that value": one would let a caller
    /// learn the secret by watching which requests move the row.
    /// </summary>
    [Fact]
    public void Setting_the_same_credential_again_still_moves_the_device()
    {
        var device = MonitoredDevice.Register(
            NewDeviceFor(AnAsset) with { SnmpCommunity = "public-ro" },
            Now,
            Actor);

        var later = Now.AddHours(1);
        device.SetSnmpCredential("public-ro", later, Actor);

        device.UpdatedAt.ShouldBe(later);
    }

    [Fact]
    public void Clearing_a_credential_that_is_not_there_moves_nothing()
    {
        var device = MonitoredDevice.Register(NewDeviceFor(AnAsset), Now, Actor);

        device.ClearSnmpCredential(Now.AddHours(1), Actor).ShouldBeFalse();
        device.UpdatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Clearing_a_credential_removes_it()
    {
        var device = MonitoredDevice.Register(
            NewDeviceFor(AnAsset) with { SnmpCommunity = "public-ro" },
            Now,
            Actor);

        device.ClearSnmpCredential(Now.AddHours(1), Actor).ShouldBeTrue();
        device.SnmpCommunity.ShouldBeNull();
        device.HasSnmpCredential.ShouldBeFalse();
    }

    [Fact]
    public void A_blank_credential_is_refused()
    {
        var device = MonitoredDevice.Register(NewDeviceFor(AnAsset), Now, Actor);

        var set = () => device.SetSnmpCredential("   ", Now, Actor);

        set.ShouldThrow<ArgumentException>();
    }

    private static NewDevice NewDeviceFor(Guid assetId) =>
        new(
            assetId,
            "SRV-0001",
            "sw-core-01",
            IPAddress.Parse("10.4.0.9"),
            MonitoredDevice.DefaultPollIntervalSeconds,
            MonitoredDevice.DefaultFailureThreshold,
            MonitoringEnabled: true,
            SnmpEnabled: false,
            SnmpSettings.DefaultPort,
            SnmpCommunity: null);
}
