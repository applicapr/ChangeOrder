using System.Globalization;
using ChangeOrder.Data.Persistence;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Presentation.Tests.Endpoints;

/// <summary>
/// Test-only repository that replaces the production raw-SQL
/// <c>UPDLOCK + HOLDLOCK</c> read with an equivalent LINQ query compatible
/// with the EF Core InMemory provider. The lock semantics are NOT exercised
/// here — the Testcontainers concurrency test owns that surface (SC-001).
/// </summary>
internal sealed class InMemoryChangeOrderRepository : IChangeOrderRepository
{
    private readonly ApplicationDbContext _dbContext;

    public InMemoryChangeOrderRepository(ApplicationDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<DomainChangeOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.ChangeOrders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DomainChangeOrder>> ListAsync(PagedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        int skip = (request.Page - 1) * request.PageSize;
        List<DomainChangeOrder> rows = await _dbContext.ChangeOrders
            .OrderByDescending(o => o.RequestDate)
            .Skip(skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        return rows;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken)
        => _dbContext.ChangeOrders.CountAsync(cancellationToken);

    public async Task AddAsync(DomainChangeOrder order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        await _dbContext.ChangeOrders.AddAsync(order, cancellationToken);
    }

    public async Task<int> GetNextSequenceForDateAsync(DateOnly dateUtc, CancellationToken cancellationToken)
    {
        string prefix = dateUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        List<string> values = await _dbContext.ChangeOrders
            .Select(o => o.OrderNumber.Value)
            .Where(v => v.StartsWith(prefix + "-", StringComparison.Ordinal))
            .ToListAsync(cancellationToken);
        int max = 0;
        foreach (string value in values)
        {
            string suffix = value.Substring(9, 2);
            if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > max)
            {
                max = parsed;
            }
        }
        return max + 1;
    }

    public Task<IdempotencyKey?> FindIdempotencyAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _dbContext.IdempotencyKeys.FirstOrDefaultAsync(k => k.Key == key, cancellationToken);
    }

    public async Task AddIdempotencyAsync(IdempotencyKey idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey);
        await _dbContext.IdempotencyKeys.AddAsync(idempotencyKey, cancellationToken);
    }

    public void Remove(DomainChangeOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        _dbContext.ChangeOrders.Remove(order);
    }
}
