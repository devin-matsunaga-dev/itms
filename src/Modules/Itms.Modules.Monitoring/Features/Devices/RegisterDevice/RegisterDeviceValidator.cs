using FluentValidation;
using Itms.Modules.Monitoring.Domain;

namespace Itms.Modules.Monitoring.Features.Devices.RegisterDevice;

/// <summary>Checks the shape of a registration before the handler runs.</summary>
/// <remarks>
/// Whether the asset exists is not checked here: it needs another module, and a validator
/// that asked would still lose the race to the unique index. The handler owns both — it
/// resolves the asset through <c>IAssetLookup</c> and refuses a second device for one asset
/// with a 409.
/// </remarks>
public sealed class RegisterDeviceValidator : AbstractValidator<RegisterDeviceRequest>
{
    /// <summary>Builds the rules.</summary>
    public RegisterDeviceValidator()
    {
        RuleFor(request => request.AssetId)
            .NotEmpty().WithMessage("Choose the asset this device is.");

        RuleFor(request => request.Hostname).MaximumLength(MonitoredDevice.HostnameMaxLength);

        RuleFor(request => request.IpAddress)
            .Must(DeviceAddress.IsAbsentOrWellFormed)
            .WithMessage("Enter a valid IPv4 or IPv6 address.");

        // The entity refuses this too, by throwing. Stated here as well so the caller gets
        // a 400 naming both fields rather than a 500 — the rule is the entity's, the
        // message is this layer's job.
        RuleFor(request => request)
            .Must(request => !string.IsNullOrWhiteSpace(request.Hostname)
                || !string.IsNullOrWhiteSpace(request.IpAddress))
            .WithName("hostname")
            .WithMessage("Give the device a hostname or an IP address.");

        RuleFor(request => request.PollIntervalSeconds)
            .InclusiveBetween(MonitoredDevice.MinPollIntervalSeconds, MonitoredDevice.MaxPollIntervalSeconds)
            .When(request => request.PollIntervalSeconds.HasValue);

        RuleFor(request => request.FailureThreshold)
            .InclusiveBetween(MonitoredDevice.MinFailureThreshold, MonitoredDevice.MaxFailureThreshold)
            .When(request => request.FailureThreshold.HasValue);

        RuleFor(request => request.SnmpPort)
            .InclusiveBetween(SnmpSettings.MinPort, SnmpSettings.MaxPort)
            .When(request => request.SnmpPort.HasValue);

        RuleFor(request => request.SnmpCommunity).MaximumLength(SnmpSettings.CommunityMaxLength);

        // Deliberately not a rule: SNMP enabled with no community string. A deployment can
        // legitimately configure the port and the flag first and hand the credential over
        // separately, which is exactly what the dedicated credential route is for.
    }
}
