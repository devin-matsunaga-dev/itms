namespace Itms.Messaging;

/// <summary>
/// Anchors the messaging assembly for reflection-based discovery. The architecture
/// tests locate this assembly through this type rather than by name string, so
/// renaming or dropping the project breaks the compile instead of silently
/// shrinking what the rules cover.
/// </summary>
public sealed class AssemblyMarker;
