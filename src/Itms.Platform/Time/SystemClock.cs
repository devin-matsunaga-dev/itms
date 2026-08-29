namespace Itms.Platform.Time;

/// <summary>The production <see cref="IClock"/>. The only place in the solution that reads the wall clock.</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
