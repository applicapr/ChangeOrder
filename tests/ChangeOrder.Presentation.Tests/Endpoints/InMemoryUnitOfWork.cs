using ChangeOrder.Data.Persistence;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace ChangeOrder.Presentation.Tests.Endpoints;

/// <summary>
/// Test-only <see cref="IUnitOfWork"/> implementation that wraps EF Core
/// InMemory. Unlike the production <c>UnitOfWork</c> it does not try to
/// translate <c>SqlException</c> codes (InMemory never raises them); it just
/// commits and returns success. Concurrency conflicts surfaced by the InMemory
/// provider are still translated to <c>DomainErrors.Order.ConcurrencyConflict</c>
/// so the FR-013 contract can be exercised end-to-end.
/// </summary>
internal sealed class InMemoryUnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;

    public InMemoryUnitOfWork(ApplicationDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public async Task<Result<int, Error>> SaveChangesWithDuplicateDetectionAsync(CancellationToken cancellationToken)
    {
        int affected = await _dbContext
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        return Result<int, Error>.Success(affected);
    }

    public async Task<Result<int, Error>> SaveChangesWithConcurrencyDetectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            int affected = await _dbContext
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
            return Result<int, Error>.Success(affected);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<int, Error>.Failure(DomainErrors.Order.ConcurrencyConflict());
        }
    }

    /// <summary>
    /// The EF Core InMemory provider does not support relational transactions,
    /// so the test double returns a no-op scope that mirrors the production
    /// contract: Commit/Rollback succeed without side effects and DisposeAsync
    /// is idempotent. This lets the WebApplicationFactory exercise the same
    /// CreateOrderHandler code path that production uses (research.md R-1)
    /// without forcing a real SQL Server dependency on every endpoint test.
    /// </summary>
    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
        => Task.FromResult<IUnitOfWorkTransaction>(new NoopTransaction());

    private sealed class NoopTransaction : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
