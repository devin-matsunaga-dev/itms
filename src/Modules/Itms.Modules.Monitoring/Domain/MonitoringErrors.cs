using Itms.Platform.Results;

namespace Itms.Modules.Monitoring.Domain;

/// <summary>
/// Every failure this module can return, written once.
/// </summary>
/// <remarks>
/// The codes are part of the API surface — clients switch on them — so they live in one
/// file where a reword is visible in review rather than being spelled out at each call
/// site that can produce them.
/// </remarks>
internal static class MonitoringErrors
{
    /// <summary>The device does not exist.</summary>
    public static Error DeviceNotFound() =>
        Error.NotFound("monitoring.device_not_found", "No such monitored device.");

    /// <summary>
    /// The asset a device was to be registered over does not exist.
    /// </summary>
    /// <remarks>
    /// <b>This is invariant 6 refusing, and it is a 400 rather than a 404.</b> The request
    /// named a route that exists and a device that does not yet — what is missing is a
    /// value in the body, which is what a validation failure means. Answering 404 would
    /// claim the device endpoint itself was not there. The per-field message puts it on the
    /// asset picker, where somebody can act on it.
    /// </remarks>
    public static Error AssetNotFound() =>
        Error.Validation(
            "monitoring.asset_not_found",
            "No such asset. A monitored device is always an asset.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["assetId"] = ["No such asset. A monitored device is always an asset."],
            });

    /// <summary>
    /// That asset is already monitored.
    /// </summary>
    /// <remarks>
    /// A conflict rather than a validation failure: the request is well formed and the
    /// asset is a perfectly good asset — it is the state of the world that refuses it. One
    /// asset has at most one device, because a second row would give one machine two
    /// monitoring states and two of everything downstream: two outage histories, two
    /// alerts, two tickets.
    /// </remarks>
    public static Error DeviceAlreadyRegistered(string assetTag) =>
        Error.Conflict(
            "monitoring.device_already_registered",
            $"Asset '{assetTag}' is already monitored. An asset has at most one monitored device.");

    /// <summary>
    /// The device would be left with neither a hostname nor an address.
    /// </summary>
    /// <remarks>
    /// Reachable through validation before a handler runs; declared here as well because
    /// the same rule is what <c>MonitoredDevice.Register</c> and <c>Update</c> refuse, and
    /// a caller that reaches the entity through a different route should get the same code.
    /// </remarks>
    public static Error DeviceUnreachable() =>
        Error.Validation(
            "monitoring.device_unreachable",
            "A monitored device needs a hostname or an IP address.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["hostname"] = ["Give the device a hostname or an IP address."],
                ["ipAddress"] = ["Give the device a hostname or an IP address."],
            });

    /// <summary>The IP address in the request is not one.</summary>
    public static Error MalformedIpAddress() =>
        Error.Validation(
            "monitoring.malformed_ip_address",
            "That is not an IP address.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["ipAddress"] = ["Enter a valid IPv4 or IPv6 address."],
            });

    /// <summary>
    /// The caller's <c>If-Match</c> named a version the device is no longer at.
    /// </summary>
    /// <remarks>
    /// A 412 rather than the 409 a lost race gets, following WP-1.5's split: the
    /// precondition failed before anything was attempted, which is what lets a client find
    /// out before it has typed a change rather than after.
    /// </remarks>
    public static Error DevicePreconditionFailed() =>
        Error.PreconditionFailed(
            "monitoring.device_conflict",
            "This device has changed since you read it. Reload and try again.");

    /// <summary>The write lost a race with a concurrent one.</summary>
    /// <remarks>
    /// The same code as the 412 deliberately, following WP-1.5: both mean "your copy is
    /// stale, reload", which is the one thing a client does about either, and the status
    /// separates them for anybody who cares which happened.
    /// </remarks>
    public static Error DeviceConflict() =>
        Error.Conflict(
            "monitoring.device_conflict",
            "This device changed while your request was being applied. Reload and try again.");
}
