using Microsoft.Extensions.Logging;

namespace Itms.Modules.Helpdesk;

/// <summary>
/// The module's log messages, source-generated. CONVENTIONS.md requires structured
/// properties, and the repo builds warnings-as-errors with CA1848 on, so every message
/// is declared here rather than formatted at the call site.
/// </summary>
internal static partial class HelpdeskLog
{
    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Information,
        Message = "Seeded the ticket reference data: {CategoryCount} categor(y/ies), {PriorityCount} priorit(y/ies).")]
    public static partial void SeededReferenceData(ILogger logger, int categoryCount, int priorityCount);
}
