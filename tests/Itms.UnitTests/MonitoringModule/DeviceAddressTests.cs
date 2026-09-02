using System.Net;
using Itms.Modules.Monitoring.Features.Devices;

namespace Itms.UnitTests.MonitoringModule;

/// <summary>
/// The address parse both device write shapes share.
/// </summary>
public sealed class DeviceAddressTests
{
    [Theory]
    [InlineData("10.4.0.9")]
    [InlineData("  10.4.0.9  ")]
    [InlineData("2001:db8::1")]
    [InlineData("::1")]
    public void A_well_formed_address_parses(string value)
    {
        DeviceAddress.TryParse(value, out var address).ShouldBeTrue();
        address.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_absent_address_parses_to_nothing(string? value)
    {
        DeviceAddress.TryParse(value, out var address).ShouldBeTrue();
        address.ShouldBeNull();
    }

    /// <summary>
    /// <c>IPAddress.TryParse</c> reads a bare integer as an address — "1" becomes
    /// 0.0.0.1 — which is never what somebody typing into an address field meant. Requiring
    /// a dot or a colon rejects it without rejecting anything real.
    /// </summary>
    [Theory]
    [InlineData("1")]
    [InlineData("2130706433")]
    [InlineData("sw-core-01")]
    [InlineData("10.4.0.999")]
    [InlineData("not an address")]
    public void A_malformed_address_is_refused(string value)
    {
        DeviceAddress.TryParse(value, out var address).ShouldBeFalse();
        address.ShouldBeNull();
        DeviceAddress.IsAbsentOrWellFormed(value).ShouldBeFalse();
    }

    /// <summary>
    /// Parsing rather than storing the text is what makes the API answer one spelling of
    /// an address rather than echoing whatever was typed — so two people cannot register
    /// what looks like two devices at one address.
    /// </summary>
    [Fact]
    public void A_parsed_address_is_the_canonical_one()
    {
        DeviceAddress.TryParse("2001:0db8:0000:0000:0000:0000:0000:0001", out var address).ShouldBeTrue();

        address.ShouldBe(IPAddress.Parse("2001:db8::1"));
        address!.ToString().ShouldBe("2001:db8::1");
    }

    /// <summary>
    /// .NET refuses a dotted-quad with leading zeros rather than guessing whether
    /// <c>010</c> means eight or ten — the octal ambiguity that has produced real
    /// access-control bypasses elsewhere. Asserted so a later "be more forgiving" change
    /// has to argue with a test.
    /// </summary>
    [Fact]
    public void A_dotted_quad_with_leading_zeros_is_refused_rather_than_guessed_at()
    {
        DeviceAddress.TryParse("010.004.000.009", out var address).ShouldBeFalse();
        address.ShouldBeNull();
    }
}
