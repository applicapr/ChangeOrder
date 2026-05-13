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
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            LogUniqueViolation(ex);
            return Result<int, Error>.Failure(DomainErrors.Order.DuplicateNumber("unknown"));
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlEx
            && (sqlEx.Number == SqlErrorUniqueConstraintViolation
                || sqlEx.Number == SqlErrorUniqueIndexViolation);
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "UNIQUE constraint violation while persisting ChangeOrder; caller should retry.")]
    private partial void LogUniqueViolation(Exception ex);
}
