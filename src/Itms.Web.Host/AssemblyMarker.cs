namespace Itms.Web.Host;

/// <summary>
/// Anchors the host assembly for reflection-based discovery, as every other project in
/// the solution does.
/// </summary>
/// <remarks>
/// The integration suite points <c>WebApplicationFactory</c> at this rather than at
/// <c>Program</c>: the AppHost declares a <c>Program</c> of its own, and a test project
/// that references both would have to choose between them with an extern alias.
/// </remarks>
public sealed class AssemblyMarker;
