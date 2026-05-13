using ChangeOrder.Domain.Errors;

namespace ChangeOrder.Domain.Abstractions;

/// <summary>
/// Transactional commit boundary for the data layer. Implementations wrap
/// <c>DbContext.SaveChangesAsync</c> and are the single mutation surface in
/// Business-layer handlers.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists pending changes. Returns the number of state entries written.
    /// </summary>
    /// <param name="cancellationToken">Token used to abort the operation.</param>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Persists pending changes and translates a SQL Server UNIQUE-constraint
    /// violation on the <c>IX_ChangeOrders_OrderNumber</c> index into a
    /// <c>DomainErrors.Order.DuplicateNumber</c> failure (research.md R-1).
    /// </summary>
    /// <param name="cancellationToken">Token used to abort the operation.</param>
    public Task<Result<int, Error>> SaveChangesWithDuplicateDetectionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Persists pending changes and translates an EF Core
    /// <c>DbUpdateConcurrencyException</c> (raised when the SQL Server
    /// <c>rowversion</c> column does not match) into a
    /// <c>DomainErrors.Order.ConcurrencyConflict</c> failure (FR-013).
    /// </summary>
    /// <param name="cancellationToken">Token used to abort the operation.</param>
    public Task<Result<int, Error>> SaveChangesWithConcurrencyDetectionAsync(CancellationToken cancellationToken);
}
