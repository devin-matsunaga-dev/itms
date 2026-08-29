using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Itms.Platform.Data;

/// <summary>
/// The one database connection a unit of work runs on, and the transaction it runs in,
/// as a module is allowed to see it.
/// </summary>
/// <remarks>
/// <para>
/// Every <see cref="DbContext"/> in a scope must be built on the <em>same</em>
/// <see cref="Connection"/>, or a "transaction" spanning two contexts is two
/// transactions and a window between them. The bus owns the implementation of that
/// rule, because the outbox write is the thing that has to commit with the caller's
/// change — but a module may not reference the bus (ARCHITECTURE.md §3, asserted by
/// <c>ModuleBoundaryTests</c>), so the shared kernel declares the shape and the
/// composition root adapts the bus's session onto it.
/// </para>
/// <para>
/// This interface deliberately exposes no way to publish an event: a module publishes
/// through <c>IEventPublisher</c>, which it receives by injection. What it gets here is
/// only the connection and the transaction its own persistence needs.
/// </para>
/// </remarks>
public interface IModuleDbSession
{
    /// <summary>The connection every context in this scope shares. Opened on first use.</summary>
    DbConnection Connection { get; }

    /// <summary>The transaction currently in flight, or <see langword="null"/> outside one.</summary>
    DbTransaction? CurrentTransaction { get; }

    /// <summary>Opens <see cref="Connection"/> if it is not already open.</summary>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The shared, open connection.</returns>
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Points <paramref name="context"/> at <see cref="CurrentTransaction"/> so its
    /// <c>SaveChanges</c> lands in the ambient transaction rather than opening its own.
    /// A no-op outside a transaction.
    /// </summary>
    /// <param name="context">A context already built on <see cref="Connection"/>.</param>
    /// <param name="cancellationToken">Cancels the enlistment.</param>
    Task EnlistAsync(DbContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> inside one transaction on the shared connection,
    /// committing if it returns and rolling back if it throws. Nested calls join the
    /// transaction already in flight; only the outermost one commits.
    /// </summary>
    /// <typeparam name="TResult">What the work produces.</typeparam>
    /// <param name="work">The unit of work. Everything it writes, published events included, commits together.</param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    /// <returns>Whatever <paramref name="work"/> returned.</returns>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default);

    /// <summary>Runs <paramref name="work"/> inside one transaction. The void-returning overload.</summary>
    /// <param name="work">The unit of work.</param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default);
}
