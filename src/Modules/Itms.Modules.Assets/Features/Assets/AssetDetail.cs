namespace Itms.Modules.Assets.Features.Assets;

/// <summary>
/// An asset together with the row version it was read or written at.
/// </summary>
/// <remarks>
/// <para>
/// The version is not part of <see cref="AssetResponse"/> because it is not part of the
/// asset: it is a fact about <em>this exchange</em>, and HTTP already has a place for
/// that. It travels to the client as an <c>ETag</c> header and comes back as
/// <c>If-Match</c> — never in the body, where a client would be tempted to store it
/// alongside the asset and send it back long after it stopped being true.
/// </para>
/// <para>
/// One type for the read and for all four lifecycle writes, because they answer with the
/// same thing: the asset as it now stands. A ticket needed a shape per write because a
/// status change and an assignment report different things about the move; an asset's
/// lifecycle operations report the asset, and the move itself is in the timeline.
/// </para>
/// <para>
/// Internal, so the shape never reaches the OpenAPI document. What the contract describes
/// is <see cref="AssetResponse"/> and the header.
/// </para>
/// </remarks>
/// <param name="Response">The asset as the client sees it.</param>
/// <param name="Version">The <c>xmin</c> row version at the moment of the exchange.</param>
internal sealed record AssetDetail(AssetResponse Response, uint Version);
