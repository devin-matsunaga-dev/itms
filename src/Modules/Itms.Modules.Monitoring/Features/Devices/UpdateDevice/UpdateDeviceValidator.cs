using FluentValidation;
using Itms.Modules.Monitoring.Domain;

namespace Itms.Modules.Monitoring.Features.Devices.UpdateDevice;

/// <summary>Checks the shape of an edit before the handler runs.</summary>
public sealed class UpdateDeviceValidator : AbstractValidator<UpdateDeviceRequest>
{
    /// <summary>Builds the rules.</summary>
    public UpdateDeviceValidator()
    {
        RuleFor(request => request.Hostname).MaximumLength(MonitoredDevice.HostnameMaxLength);

        RuleFor(request => request.IpAddress)
            .Must(DeviceAddress.IsAbsentOrWellFormed)
            .WithMessage("Enter a valid IPv4 or IPv6 address.");

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
    }
}
