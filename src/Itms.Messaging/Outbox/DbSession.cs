using System.Data;
using System.Data.Common;
using Itms.Messaging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Itms.Messaging.Outbox;

/// <summary>
/// The scoped owner of one connection and at most one transaction. Registered per
/// scope, so a web request, a dispatcher pass, and a test each get their own.
/// </summary>
/// <param name="dataSource">The pooled data source Aspire supplies the host.</param>
public sealed class DbSession(NpgsqlDataSource dataSource) : IDbSession, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    // The contexts pointed at the current transaction. EF keeps its own wrapper around
    // a transaction it was handed, and that wrapper outlives the DbTransaction unless it
    // is cleared — after which the next SaveChanges on the context uses a disposed one.
    private readonly List<DbContext> _enlisted = [];
    private NpgsqlConnection? _connection;
    private DbTransaction? _transaction;

    /// <inheritdoc />
    public DbConnection Connection => _connection ??= _dataSource.CreateConnection();

    /// <inheritdoc />
    public DbTransaction? CurrentTransaction => _transaction;

    /// <inheritdoc />
    public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = Connection;
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        return connection;
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        // A nested call joins the transaction already in flight. Only the outermost
        // one commits, so a consumer calling another cannot half-commit the outer work.
        if (_transaction is not null)
        {
            return await work(cancellationToken).ConfigureAwait(false);
        }

        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        _transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await work(cancellationToken).ConfigureAwait(false);
            await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            // Rollback is best-effort: if the connection is already gone the transaction
            // died with it, and throwing here would replace the real failure with a lesser one.
            try
            {
                await _transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
            }
            catch (NpgsqlException)
            {
            }

            throw;
        }
        finally
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
            await ClearEnlistmentsAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        await ExecuteInTransactionAsync<object?>(
            async ct =>
            {
                await work(ct).ConfigureAwait(false);
                return null;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EnlistAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_transaction is null)
        {
            return;
        }

        // EF tracks the transaction per context. Re-enlisting the same one is a no-op,
        // which matters because every publish on a scope calls through here.
        if (context.Database.CurrentTransaction?.GetDbTransaction() == _transaction)
        {
            return;
        }

        await context.Database.UseTransactionAsync(_transaction, cancellationToken).ConfigureAwait(false);
        _enlisted.Add(context);
    }

    private async Task ClearEnlistmentsAsync()
    {
        foreach (var context in _enlisted)
        {
            // Passing null detaches the context from the transaction that has just ended,
            // so its next operation opens a fresh one instead of reusing a disposed handle.
            await context.Database.UseTransactionAsync(null, CancellationToken.None).ConfigureAwait(false);
        }

        _enlisted.Clear();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _enlisted.Clear();

        if (_transaction is not null)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
    }
}
