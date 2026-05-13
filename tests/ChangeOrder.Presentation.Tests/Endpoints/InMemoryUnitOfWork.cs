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
}
