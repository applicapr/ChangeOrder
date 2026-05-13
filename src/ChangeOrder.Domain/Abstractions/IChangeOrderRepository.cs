using ChangeOrder.Domain.Entities;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Domain.Abstractions;

/// <summary>
/// Persistence contract for the <c>ChangeOrder</c> aggregate. The Data layer
/// owns the implementation; the Business layer consumes this contract only.
/// </summary>
public interface IChangeOrderRepository
{
    /// <summary>Loads a single order by id; returns <c>null</c> when not found or soft-deleted.</summary>
    public Task<DomainChangeOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a single order by id for read-only consumption (no change tracking);
    /// returns <c>null</c> when not found or soft-deleted. Use this from query
    /// handlers; command handlers that mutate the aggregate must use
    /// <see cref="GetByIdAsync"/> instead.
    /// </summary>
    public Task<DomainChangeOrder?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a page of non-deleted orders together with the total row count
    /// for the same predicate, computed in a single query plan to avoid the
    /// duplicate predicate construction that an independent List+Count pair
    /// would incur.
    /// </summary>
    public Task<(IReadOnlyList<DomainChangeOrder> Items, int Total)> ListPagedAsync(
        PagedRequest request,
        CancellationToken cancellationToken);

    /// <summary>Adds a new order to the change tracker; commit happens through <see cref="IUnitOfWork"/>.</summary>
    public Task AddAsync(DomainChangeOrder order, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the next daily sequence for <paramref name="dateUtc"/> under a
    /// pessimistic <c>UPDLOCK + HOLDLOCK</c> read (research.md R-1). The caller
    /// is responsible for retrying on UNIQUE-constraint collisions.
    /// </summary>
    public Task<int> GetNextSequenceForDateAsync(DateOnly dateUtc, CancellationToken cancellationToken);

    /// <summary>Looks up the persisted idempotency record for a given client key; <c>null</c> on miss.</summary>
    public Task<IdempotencyKey?> FindIdempotencyAsync(string key, CancellationToken cancellationToken);

    /// <summary>Adds a new idempotency record to the change tracker; commit happens through <see cref="IUnitOfWork"/>.</summary>
    public Task AddIdempotencyAsync(IdempotencyKey idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Removes an order (the Data-layer interceptor turns this into a soft delete).</summary>
    public void Remove(DomainChangeOrder order);
}
