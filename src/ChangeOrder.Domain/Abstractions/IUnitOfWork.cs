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
}
