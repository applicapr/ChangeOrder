using ChangeOrder.Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChangeOrder.Data.Repositories;

/// <summary>
/// Hard-deletes expired <c>IdempotencyKey</c> rows. Lives in the Data layer
/// because the operation hits <see cref="ApplicationDbContext"/> directly; the
/// Host background service invokes it on a scoped instance every hour.
/// </summary>
/// <remarks>
/// The table is NOT soft-deletable (research.md R-2): cleanup is a real
/// <c>DELETE</c> bypassing the <c>AuditInterceptor</c> rules. The 24h retention
/// window is enforced here as a constant; if it ever needs to be configurable
/// surface an <c>IOptions&lt;IdempotencyOptions&gt;</c> binding.
/// </remarks>
public sealed partial class IdempotencyKeyCleanupRepository
{
    /// <summary>Retention window for idempotency rows (research.md R-2).</summary>
    public static readonly TimeSpan RetentionWindow = TimeSpan.FromHours(24);

    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<IdempotencyKeyCleanupRepository> _logger;

    /// <summary>Builds the cleanup repository bound to the given context.</summary>
    public IdempotencyKeyCleanupRepository(
        ApplicationDbContext dbContext,
        ILogger<IdempotencyKeyCleanupRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Deletes every idempotency row older than <see cref="RetentionWindow"/>
    /// (relative to <see cref="DateTime.UtcNow"/>) and returns the affected
    /// row count.
    /// </summary>
    public async Task<int> RemoveExpiredAsync(CancellationToken cancellationToken)
    {
        DateTime threshold = DateTime.UtcNow - RetentionWindow;
        try
        {
            int affected = await _dbContext.IdempotencyKeys
                .Where(k => k.CreatedAt < threshold)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            if (affected > 0)
            {
                LogExpiredRowsRemoved(affected, threshold);
            }
            return affected;
        }
        catch (Exception ex)
        {
            LogCleanupFailed(ex, threshold);
            throw;
        }
    }

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Information,
        Message = "Idempotency cleanup removed {Count} row(s) older than {Threshold:o} (research.md R-2).")]
    private partial void LogExpiredRowsRemoved(int count, DateTime threshold);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Error,
        Message = "Idempotency cleanup failed while pruning rows older than {Threshold:o}.")]
    private partial void LogCleanupFailed(Exception ex, DateTime threshold);
}
