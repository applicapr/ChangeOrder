using System.Globalization;
using ChangeOrder.Data.Persistence;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IChangeOrderRepository"/>. The
/// <see cref="GetNextSequenceForDateAsync"/> method uses the
/// <c>UPDLOCK + HOLDLOCK</c> raw-SQL strategy described in research.md R-1.
/// </summary>
public sealed partial class ChangeOrderRepository : IChangeOrderRepository
{
    private const int SqlErrorDeadlockVictim = 1205;

    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ChangeOrderRepository> _logger;

    /// <summary>Builds a repository bound to the given context.</summary>
    public ChangeOrderRepository(ApplicationDbContext dbContext, ILogger<ChangeOrderRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<DomainChangeOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.ChangeOrders
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<DomainChangeOrder?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.ChangeOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<DomainChangeOrder> Items, int Total)> ListPagedAsync(
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        int skip = (request.Page - 1) * request.PageSize;
        IQueryable<DomainChangeOrder> query = _dbContext.ChangeOrders.AsNoTracking();
        if (!string.IsNullOrEmpty(request.OrderNumberFilter))
        {
            // OrderNumber values follow the canonical "yyyyMMdd-##" form, so no LIKE
            // metacharacter ('%', '_', '[') can appear in the filter — passing the
            // raw prefix to EF.Functions.Like is safe.
            string pattern = request.OrderNumberFilter + "%";
            query = query.Where(o => EF.Functions.Like(o.OrderNumber.Value, pattern));
        }

        int total = await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        List<DomainChangeOrder> rows = await query
            .OrderByDescending(o => o.RequestDate)
            .Skip(skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (rows, total);
    }

    /// <inheritdoc/>
    public async Task AddAsync(DomainChangeOrder order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        await _dbContext.ChangeOrders.AddAsync(order, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<int, Error>> GetNextSequenceForDateAsync(DateOnly dateUtc, CancellationToken cancellationToken)
    {
        string prefix = dateUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        const string Sql = """
            SELECT ISNULL(MAX(CAST(RIGHT(OrderNumber, 2) AS INT)), 0) + 1
            FROM   dbo.ChangeOrders WITH (UPDLOCK, HOLDLOCK)
            WHERE  OrderNumber LIKE @datePrefix + '-%'
            """;

        IDbContextTransaction? currentTransaction = _dbContext.Database.CurrentTransaction;

        // When a transaction is already active on the DbContext, the underlying
        // connection is open and bound to that transaction (research.md R-1).
        // Calling OpenConnectionAsync/CloseConnectionAsync would bump EF Core's
        // refcount and is unnecessary; we only manage the connection lifetime
        // when the caller has NOT opened an explicit transaction (e.g. tests).
        bool ownsConnectionLifetime = currentTransaction is null;
        if (ownsConnectionLifetime)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using System.Data.Common.DbCommand command = _dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = Sql;
            command.Transaction = currentTransaction?.GetDbTransaction();
            command.Parameters.Add(new SqlParameter("@datePrefix", prefix));

            object? scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            int next = scalar is null or DBNull ? 1 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
            return Result<int, Error>.Success(next);
        }
        catch (SqlException ex) when (ex.Number == SqlErrorDeadlockVictim)
        {
            // C1: 16 concurrent workers can deadlock on the UPDLOCK+HOLDLOCK
            // key-range scan over an empty daily prefix — SQL Server picks one
            // session as the deadlock victim and raises 1205 from the SELECT
            // itself, before SaveChanges. Map to a retryable domain failure so
            // CreateOrderHandler can roll back the current transaction and try
            // again under a fresh lock acquisition order.
            LogDeadlockOnSequenceRead(ex);
            return Result<int, Error>.Failure(DomainErrors.Order.DeadlockVictim());
        }
        finally
        {
            if (ownsConnectionLifetime)
            {
                await _dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "SQL Server picked this session as the deadlock victim (1205) while reading the daily OrderNumber sequence; caller should retry.")]
    private partial void LogDeadlockOnSequenceRead(Exception ex);

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
