using System.Data.Common;
using Itms.Messaging.Abstractions;
using Itms.Platform.Data;
using Microsoft.EntityFrameworkCore;

namespace Itms.Web.Host.Data;

/// <summary>
/// Presents the bus's <see cref="IDbSession"/> to modules as
/// <see cref="IModuleDbSession"/>.
/// </summary>
/// <remarks>
/// <para>
/// A module may not reference <c>Itms.Messaging</c> (ARCHITECTURE.md §3, asserted by
/// <c>ModuleBoundaryTests</c>), but every module context must be built on the one
/// connection the session owns or the outbox write and the change it announces are two
/// transactions rather than one. The composition root is the only place that can see
/// both sides, so the adapter lives here — twelve lines in the host instead of a
/// boundary hole in every module.
/// </para>
/// <para>
/// It delegates rather than reimplements: enlistment tracking, nested transactions, and
/// rollback all stay in <c>DbSession</c>, which is the one place they are tested.
/// </para>
/// </remarks>
/// <param name="session">The scoped session the bus owns.</param>
internal sealed class ModuleDbSessionAdapter(IDbSession session) : IModuleDbSession
{
    /// <inheritdoc />
    public DbConnection Connection => session.Connection;

    /// <inheritdoc />
    public DbTransaction? CurrentTransaction => session.CurrentTransaction;

    /// <inheritdoc />
    public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default) =>
        session.OpenAsync(cancellationToken);

    /// <inheritdoc />
    public Task EnlistAsync(DbContext context, CancellationToken cancellationToken = default) =>
        session.EnlistAsync(context, cancellationToken);

    /// <inheritdoc />
    public Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default) =>
        session.ExecuteInTransactionAsync(work, cancellationToken);

    /// <inheritdoc />
    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default) =>
        session.ExecuteInTransactionAsync(work, cancellationToken);
}
