namespace Itms.Modules.Audit;

/// <summary>
/// Anchors the Audit module assembly for reflection-based discovery. The architecture
/// tests and convention-based registration locate this assembly through this
/// type, so neither has to name it by string.
/// </summary>
public sealed class AssemblyMarker;
