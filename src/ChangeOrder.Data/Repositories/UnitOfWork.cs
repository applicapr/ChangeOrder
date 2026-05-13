using ChangeOrder.Data.Persistence;
using ChangeOrder.Domain.Abstractions;

namespace ChangeOrder.Data.Repositories;

/// <summary>
/// Default <see cref="IUnitOfWork"/> implementation: a thin wrapper around
/// <c>ApplicationDbContext.SaveChangesAsync</c>.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>Builds a UoW bound to the given context.</summary>
    public UnitOfWork(ApplicationDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
