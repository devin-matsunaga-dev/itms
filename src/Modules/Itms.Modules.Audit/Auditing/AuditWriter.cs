using Itms.Contracts.Auditing;
using Itms.Platform.Identity;
using Itms.Platform.Time;

namespace Itms.Modules.Audit.Auditing;

/// <summary>
/// The public <see cref="IAuditWriter"/>: how a module records a mutation that does not
/// warrant a domain event (ARCHITECTURE.md §8).
/// </summary>
/// <remarks>
/// The actor, the timestamp, and the source address are taken from the ambient request
/// and never from the caller. A handler cannot state who it was, cannot backdate an
/// entry, and cannot claim a different address — which is the property that makes the
/// trail worth reading.
/// </remarks>
/// <param name="recorder">The single write path into the table.</param>
/// <param name="currentUser">Who is making the current request.</param>
/// <param name="clock">The system's only source of "now".</param>
internal sealed class AuditWriter(
    AuditRecorder recorder,
    ICurrentUser currentUser,
    IClock clock) : IAuditWriter
{
    /// <inheritdoc />
    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return recorder.RecordAsync(
            clock.UtcNow,
            currentUser.UserId,
            currentUser.DisplayName,
            entry.Action,
            entry.EntityType,
            entry.EntityId,
            currentUser.IpAddress,
            entry.Changes,
            cancellationToken);
    }
}
