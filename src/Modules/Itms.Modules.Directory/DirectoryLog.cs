using Microsoft.Extensions.Logging;

namespace Itms.Modules.Directory;

/// <summary>
/// The module's log messages, source-generated. CONVENTIONS.md requires structured
/// properties, and the repo builds warnings-as-errors with CA1848 on, so every message
/// is declared here rather than formatted at the call site.
/// </summary>
internal static partial class DirectoryLog
{
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Location {LocationId} renamed; {DescendantCount} descendant path(s) rewritten.")]
    public static partial void SubtreeRewritten(ILogger logger, Guid locationId, int descendantCount);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Location {LocationId} moved under {ParentId}; {DescendantCount} descendant path(s) rewritten.")]
    public static partial void LocationMoved(ILogger logger, Guid locationId, Guid? parentId, int descendantCount);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Information,
        Message = "Location {LocationId} deleted.")]
    public static partial void LocationDeleted(ILogger logger, Guid locationId);

    [LoggerMessage(
        EventId = 1103,
        Level = LogLevel.Information,
        Message = "Seeded the development directory: {DepartmentCount} department(s), {LocationCount} location(s).")]
    public static partial void SeededDirectory(ILogger logger, int departmentCount, int locationCount);
}
