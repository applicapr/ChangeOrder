namespace ChangeOrder.Domain.Abstractions;

/// <summary>
/// Explicit transactional scope opened by <see cref="IUnitOfWork.BeginTransactionAsync"/>.
/// The scope is required by research.md R-1 so that the
/// <c>UPDLOCK + HOLDLOCK</c> read in <c>GetNextSequenceForDateAsync</c> and the
/// subsequent <c>INSERT</c> run inside the same physical transaction, keeping
/// the row-lock held across both statements.
/// </summary>
/// <remarks>
/// Standard <c>using</c> semantics: <see cref="IAsyncDisposable.DisposeAsync"/>
/// rolls back the transaction if <see cref="CommitAsync"/> was not called.
/// </remarks>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    /// <summary>Commits the underlying database transaction.</summary>
    /// <param name="cancellationToken">Token used to abort the operation.</param>
    public Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>Rolls back the underlying database transaction.</summary>
    /// <param name="cancellationToken">Token used to abort the operation.</param>
    public Task RollbackAsync(CancellationToken cancellationToken);
}
