using ChangeOrder.Data.Persistence;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Errors;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChangeOrder.Data.Repositories;

/// <summary>
/// Default <see cref="IUnitOfWork"/> implementation: a thin wrapper around
/// <c>ApplicationDbContext.SaveChangesAsync</c>.
/// </summary>
public sealed partial class UnitOfWork : IUnitOfWork
{
    private const int SqlErrorUniqueIndexViolation = 2601;
    private const int SqlErrorUniqueConstraintViolation = 2627;
    private const int SqlErrorDeadlockVictim = 1205;

    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UnitOfWork> _logger;

    /// <summary>Builds a UoW bound to the given context.</summary>
    public UnitOfWork(ApplicationDbContext dbContext, ILogger<UnitOfWork> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction efTx = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        return new EfUnitOfWorkTransaction(efTx, _logger);
    }

    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<Result<int, Error>> SaveChangesWithDuplicateDetectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            int affected = await _dbContext
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
            return Result<int, Error>.Success(affected);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex) || IsDeadlockVictim(ex))
        {
            LogUniqueViolation(ex);
            // Detach all entities from the failed attempt so the caller can retry
            // without tripping EF's IdentityMap on the next AddAsync with the same
            // PK (e.g. IdempotencyKey when CreateOrderHandler loops on UNIQUE
            // collisions of OrderNumber). Deadlocks (1205) are also retryable
            // under the same loop because the SELECT-then-INSERT pattern with
            // UPDLOCK+HOLDLOCK can deadlock between concurrent inserts on the
            // same daily prefix (research.md R-1).
            _dbContext.ChangeTracker.Clear();
            return Result<int, Error>.Failure(DomainErrors.Order.DuplicateNumber("unknown"));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<int, Error>> SaveChangesWithConcurrencyDetectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            int affected = await _dbContext
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
            return Result<int, Error>.Success(affected);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            LogConcurrencyConflict(ex);
            return Result<int, Error>.Failure(DomainErrors.Order.ConcurrencyConflict());
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlEx
            && (sqlEx.Number == SqlErrorUniqueConstraintViolation
                || sqlEx.Number == SqlErrorUniqueIndexViolation);
    }

    private static bool IsDeadlockVictim(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlEx
            && sqlEx.Number == SqlErrorDeadlockVictim;
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "UNIQUE constraint violation while persisting ChangeOrder; caller should retry.")]
    private partial void LogUniqueViolation(Exception ex);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "ROWVERSION mismatch while persisting ChangeOrder; client must refetch and retry (FR-013).")]
    private partial void LogConcurrencyConflict(Exception ex);
}
