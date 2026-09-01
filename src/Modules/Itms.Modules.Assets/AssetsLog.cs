using Microsoft.Extensions.Logging;

namespace Itms.Modules.Assets;

/// <summary>
/// The module's log messages, source-generated. CONVENTIONS.md requires structured
/// properties, and the repo builds warnings-as-errors with CA1848 on, so every message is
/// declared here rather than formatted at the call site.
/// </summary>
internal static partial class AssetsLog
{
    [LoggerMessage(
        EventId = 2200,
        Level = LogLevel.Information,
        Message = "Seeded the asset reference data: {TypeCount} type(s), {StatusCount} status(es).")]
    public static partial void SeededReferenceData(ILogger logger, int typeCount, int statusCount);
}
