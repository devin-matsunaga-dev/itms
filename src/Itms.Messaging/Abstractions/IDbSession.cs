using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Itms.Messaging.Abstractions;

/// <summary>
/// The one database connection a unit of work runs on, and the transaction it runs in.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes "the publisher enrols in the caller's transaction" true rather
/// than aspirational. Every <see cref="DbContext"/> in a scope — the outbox's, and from
/// Phase 1 each module's — is built on the <em>same</em> <see cref="Connection"/>, so one
/// <see cref="DbTransaction"/> spans all of them. Without that they would each open their
/// own connection and a "transaction" across two contexts would be two transactions and a
/// window between them.
/// </para>
/// <para>
/// A module consumer does not normally touch this directly: it wraps its work in
/// <see cref="ExecuteInTransactionAsync"/> and publishes inside. The connection is opened
/// lazily, so a request that never touches the database never pays for one.
/// </para>
/// </remarks>
public interface IDbSession
{
    /// <summary>The connection every context in this scope shares. Opened on first use.</summary>
    DbConnection Connection { get; }

    /// <summary>
    /// The transaction currently in flight, or <see langword="null"/> outside one.
    /// <see cref="IEventPublisher"/> refuses to publish when this is null.
    /// </summary>
    DbTransaction? CurrentTransaction { get; }

    /// <summary>Opens <see cref="Connection"/> if it is not already open.</summary>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The shared, open connection.</returns>
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> inside one transaction on the shared connection,
    /// committing if it returns and rolling back if it throws.
    /// </summary>
    /// <typeparam name="TResult">What the work produces.</typeparam>
    /// <param name="work">The unit of work. Everything it writes, including published events, commits together.</param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>Whatever <paramref name="work"/> returned.</returns>
    /// <remarks>
    /// Nested calls join the transaction already in flight rather than opening a second
    /// one, so a consumer may call another without either needing to know which is outermost.
    /// Only the outermost call commits.
    /// </remarks>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default);

    /// <summary>Runs <paramref name="work"/> inside one transaction. The void-returning overload.</summary>
    /// <param name="work">The unit of work.</param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Points <paramref name="context"/> at <see cref="CurrentTransaction"/> so its
    /// <c>SaveChanges</c> lands in the ambient transaction rather than opening its own.
    /// </summary>
    /// <param name="context">A context already built on <see cref="Connection"/>.</param>
    /// <param name="cancellationToken">Cancels the enlistment.</param>
    Task EnlistAsync(DbContext context, CancellationToken cancellationToken = default);
}
