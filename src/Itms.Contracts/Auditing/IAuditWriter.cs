namespace Itms.Contracts.Auditing;

/// <summary>
/// One field's before and after value, as display text. Audit stores changed fields
/// only (ARCHITECTURE.md §8) — a full row snapshot makes the trail unreadable and
/// stores the unchanged columns forever.
/// </summary>
/// <param name="Before">The prior value, or <see langword="null"/> when the field was unset or the entity is new.</param>
/// <param name="After">The new value, or <see langword="null"/> when the field was cleared.</param>
public sealed record AuditFieldChange(string? Before, string? After);

/// <summary>
/// A request to record one auditable action. The actor, the timestamp, and the source
/// IP are filled in by the Audit module from the ambient request — a caller cannot
/// state who it was, which is the point.
/// </summary>
/// <param name="Action">What happened, as a stable identifier such as <c>ticket.priority_changed</c>.</param>
/// <param name="EntityType">The kind of entity, such as <c>Ticket</c>.</param>
/// <param name="EntityId">The entity's id, as text, because not every audited entity is keyed by a Guid.</param>
/// <param name="Changes">The changed fields, keyed by field name. Empty for actions that change nothing, such as a failed login.</param>
public sealed record AuditEntry(
    string Action,
    string EntityType,
    string EntityId,
    IReadOnlyDictionary<string, AuditFieldChange>? Changes = null);

/// <summary>
/// Writes audit entries for mutations that do not warrant a domain event
/// (ARCHITECTURE.md §8). Most auditing happens by consuming events; this is the
/// escape hatch for the rest, called from inside the owning module's handler.
/// </summary>
/// <remarks>
/// There is deliberately no read, update, or delete member here, and there is no
/// other interface that has one. Invariant 10: audit entries are never modified or
/// deleted through any code path in this system.
/// </remarks>
public interface IAuditWriter
{
    /// <summary>
    /// Records <paramref name="entry"/>. Enrolls in the caller's transaction, so an
    /// action that is rolled back leaves no audit row claiming it happened.
    /// </summary>
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken);
}
