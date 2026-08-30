using Itms.Contracts.Auditing;
using Itms.Modules.Audit.Domain;
using Itms.Modules.Audit.Persistence;
using Itms.Platform.Data;
using Itms.Platform.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Itms.Modules.Audit.Auditing;

/// <summary>
/// The one place an audit row is written. Everything that audits — the public
/// <see cref="IAuditWriter"/> and the domain-event consumer — goes through here, so
/// there is a single answer to "how does a row get into that table".
/// </summary>
/// <remarks>
/// <para>
/// The write joins the caller's transaction when there is one, so an action that rolls
/// back leaves no row claiming it happened. When there is none — a failed sign-in never
/// opens one, because it changes nothing — it opens its own and commits, because the
/// absence of a state change is exactly when the audit row is the only evidence.
/// </para>
/// <para>
/// It offers no update and no delete, and nothing else in the module does either
/// (invariant 10).
/// </para>
/// </remarks>
/// <param name="database">The audit context, built on the shared session connection.</param>
/// <param name="session">The ambient unit of work.</param>
/// <param name="clock">The system's only source of "now".</param>
/// <param name="logger">Structured log sink.</param>
internal sealed class AuditRecorder(
    AuditDbContext database,
    IModuleDbSession session,
    IClock clock,
    ILogger<AuditRecorder> logger)
{
    /// <summary>Appends one row.</summary>
    /// <param name="occurredAt">When the audited thing happened (UTC).</param>
    /// <param name="actorId">Who did it, or <see langword="null"/> for the system.</param>
    /// <param name="actorName">Their display name at the time, or <see langword="null"/>.</param>
    /// <param name="action">The stable action identifier.</param>
    /// <param name="entityType">The kind of entity acted on.</param>
    /// <param name="entityId">The entity's id, as text.</param>
    /// <param name="sourceIp">The caller's address, or <see langword="null"/> outside a request.</param>
    /// <param name="changes">The changed fields, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task RecordAsync(
        DateTimeOffset occurredAt,
        Guid? actorId,
        string? actorName,
        string action,
        string entityType,
        string entityId,
        string? sourceIp,
        IReadOnlyDictionary<string, AuditFieldChange>? changes,
        CancellationToken cancellationToken)
    {
        var record = AuditRecord.Create(
            occurredAt,
            actorId,
            actorName,
            action,
            entityType,
            entityId,
            sourceIp,
            AuditChangeSerializer.Serialize(changes),
            clock.UtcNow);

        try
        {
            await session.ExecuteInTransactionAsync(
                async token =>
                {
                    await session.EnlistAsync(database, token).ConfigureAwait(false);
                    await database.AppendAsync(record, token).ConfigureAwait(false);
                    await database.SaveChangesAsync(token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // The tracker must not carry this row out of the unit of work that wrote it.
            // The outbox dispatcher resolves every consumer in a batch from one scope, so
            // a row left Added by a consumer that threw would be inserted again by the
            // next message's save — a duplicate entry for an action that never committed.
            database.Entry(record).State = EntityState.Detached;
        }

        AuditLog.Recorded(logger, action, entityType, entityId, actorId);
    }
}
