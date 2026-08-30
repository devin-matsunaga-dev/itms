using Microsoft.Extensions.Logging;

namespace Itms.Modules.Audit;

/// <summary>
/// The module's log messages, source-generated so the repo's warnings-as-errors build
/// does not trip CA1848 on a path that runs once per audited action.
/// </summary>
internal static partial class AuditLog
{
    /// <summary>Records that a row was appended. The row itself is the evidence; this is for tracing.</summary>
    /// <param name="logger">The sink.</param>
    /// <param name="action">The action identifier.</param>
    /// <param name="entityType">The kind of entity acted on.</param>
    /// <param name="entityId">The entity's id.</param>
    /// <param name="actorId">Who did it, or <see langword="null"/> for the system.</param>
    [LoggerMessage(
        EventId = 700,
        Level = LogLevel.Debug,
        Message = "Audited {Action} on {EntityType} {EntityId} by {ActorId}.")]
    public static partial void Recorded(ILogger logger, string action, string entityType, string entityId, Guid? actorId);
}
