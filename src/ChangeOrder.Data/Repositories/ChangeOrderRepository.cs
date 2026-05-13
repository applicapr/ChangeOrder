using System.Globalization;
using ChangeOrder.Data.Persistence;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IChangeOrderRepository"/>. The
/// <see cref="GetNextSequenceForDateAsync"/> method uses the
/// <c>UPDLOCK + HOLDLOCK</c> raw-SQL strategy described in research.md R-1.
/// </summary>
public sealed class ChangeOrderRepository : IChangeOrderRepository
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>Builds a repository bound to the given context.</summary>
    public ChangeOrderRepository(ApplicationDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<DomainChangeOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.ChangeOrders
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DomainChangeOrder>> ListAsync(PagedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        int skip = (request.Page - 1) * request.PageSize;
        IQueryable<DomainChangeOrder> query = _dbContext.ChangeOrders;
        if (!string.IsNullOrEmpty(request.OrderNumberFilter))
        {
            string filter = request.OrderNumberFilter;
            query = query.Where(o => o.OrderNumber.Value.StartsWith(filter));
        }

        List<DomainChangeOrder> rows = await query
            .OrderByDescending(o => o.RequestDate)
            .Skip(skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows;
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(string? orderNumberFilter, CancellationToken cancellationToken)
    {
        IQueryable<DomainChangeOrder> query = _dbContext.ChangeOrders;
        if (!string.IsNullOrEmpty(orderNumberFilter))
        {
            string filter = orderNumberFilter;
            query = query.Where(o => o.OrderNumber.Value.StartsWith(filter));
        }

        return await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddAsync(DomainChangeOrder order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        await _dbContext.ChangeOrders.AddAsync(order, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> GetNextSequenceForDateAsync(DateOnly dateUtc, CancellationToken cancellationToken)
    {
        string prefix = dateUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        const string Sql = """
            SELECT ISNULL(MAX(CAST(RIGHT(OrderNumber, 2) AS INT)), 0) + 1
            FROM   dbo.ChangeOrders WITH (UPDLOCK, HOLDLOCK)
            WHERE  OrderNumber LIKE @datePrefix + '-%'
            """;

        SqlParameter datePrefix = new("@datePrefix", prefix);
        await using System.Data.Common.DbCommand command = _dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = Sql;
        command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.Parameters.Add(datePrefix);

        await _dbContext.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            object? scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return scalar is null or DBNull ? 1 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<IdempotencyKey?> FindIdempotencyAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        return await _dbContext.IdempotencyKeys
            .FirstOrDefaultAsync(k => k.Key == key, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddIdempotencyAsync(IdempotencyKey idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey);
        await _dbContext.IdempotencyKeys.AddAsync(idempotencyKey, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Remove(DomainChangeOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        _dbContext.ChangeOrders.Remove(order);
    }
}
