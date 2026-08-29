using Microsoft.Extensions.Logging;

namespace Itms.Messaging.Outbox;

/// <summary>
/// The bus's log messages, source-generated.
/// </summary>
/// <remarks>
/// Written as <c>[LoggerMessage]</c> partials rather than as <c>logger.LogXxx</c> calls
/// because the repo builds warnings-as-errors and CA1848 is right about the reason: the
/// dispatcher logs on a hot path, and the generated delegates neither box their
/// arguments nor evaluate them when the level is disabled.
/// </remarks>
internal static partial class MessagingLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "Staged {EventType} {EventId} for dispatch")]
    public static partial void EventStaged(this ILogger logger, string eventType, Guid eventId);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "Consumer {Consumer} consumed {EventId}")]
    public static partial void ConsumerSucceeded(this ILogger logger, string consumer, Guid eventId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Consumer {Consumer} failed on {EventId}; it will be retried")]
    public static partial void ConsumerFailed(this ILogger logger, Exception exception, string consumer, Guid eventId);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Outbox message {EventId} has unknown event type {EventType}; leaving it for a build that knows it")]
    public static partial void UnknownEventType(this ILogger logger, Guid eventId, string eventType);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Error,
        Message = "Outbox message {EventId} exhausted {MaxAttempts} attempts and was parked: {Error}")]
    public static partial void MessageDeadLettered(this ILogger logger, Guid eventId, int maxAttempts, string error);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Outbox dispatcher started with batch size {BatchSize} and poll interval {PollInterval}")]
    public static partial void DispatcherStarted(this ILogger logger, int batchSize, TimeSpan pollInterval);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "Outbox dispatcher stopped")]
    public static partial void DispatcherStopped(this ILogger logger);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Error,
        Message = "Outbox dispatcher pass failed; retrying after {PollInterval}")]
    public static partial void DispatcherPassFailed(this ILogger logger, Exception exception, TimeSpan pollInterval);
}
