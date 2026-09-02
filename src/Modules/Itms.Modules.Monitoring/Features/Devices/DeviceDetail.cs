namespace Itms.Modules.Monitoring.Features.Devices;

/// <summary>
/// A device together with the row version it was read or written at.
/// </summary>
/// <remarks>
/// The version is not part of <see cref="DeviceResponse"/> because it is not part of the
/// device: it is a fact about <em>this exchange</em>, and HTTP already has a place for
/// that. It travels to the client as an <c>ETag</c> header and comes back as
/// <c>If-Match</c> — never in the body, where a client would be tempted to store it
/// alongside the device and send it back long after it stopped being true. This follows
/// <c>AssetDetail</c>, for the same reasons.
/// </remarks>
/// <param name="Response">The device as the client sees it.</param>
/// <param name="Version">The <c>xmin</c> row version at the moment of the exchange.</param>
internal sealed record DeviceDetail(DeviceResponse Response, uint Version);
