using ChangeOrder.Domain.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace ChangeOrder.Data.Repositories;

/// <summary>
/// EF Core-backed implementation of <see cref="IUnitOfWorkTransaction"/>.
/// Wraps an <see cref="IDbContextTransaction"/> and enforces the
/// "rollback-if-not-committed" semantics that <c>using</c> blocks expect
/// (research.md R-1).
/// </summary>
internal sealed partial class EfUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    private readonly IDbContextTransaction _transaction;
    private readonly ILogger _logger;
    private bool _committed;
    private bool _disposed;

    public EfUnitOfWorkTransaction(IDbContextTransaction transaction, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(logger);
        _transaction = transaction;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _committed = true;
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        // Mark as committed so DisposeAsync does not attempt a second rollback.
        _committed = true;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!_committed)
        {
            // Standard `using` semantics: rollback if Commit was not explicitly
            // called. Catch and log any rollback error here so DisposeAsync does
            // not mask the outer exception that escaped the using block.
            try
            {
                await _transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogRollbackOnDisposeFailed(ex);
            }
        }

        await _transaction.DisposeAsync().ConfigureAwait(false);
    }

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "Auto-rollback on transaction dispose failed; the underlying transaction may already be aborted.")]
    private partial void LogRollbackOnDisposeFailed(Exception ex);
}
