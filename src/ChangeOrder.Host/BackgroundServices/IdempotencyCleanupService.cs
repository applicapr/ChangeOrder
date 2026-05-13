using ChangeOrder.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChangeOrder.Host.BackgroundServices;

/// <summary>
/// Hosted service that prunes expired <c>IdempotencyKey</c> rows on a fixed
/// hourly cadence (research.md R-2). The actual <c>DELETE</c> happens inside
/// <see cref="IdempotencyKeyCleanupRepository"/>; this class only owns the
/// scheduling, scope management and lifetime concerns.
/// </summary>
public sealed partial class IdempotencyCleanupService : BackgroundService
{
    /// <summary>Cleanup cadence — once per hour (research.md R-2).</summary>
    public static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdempotencyCleanupService> _logger;

    /// <summary>Builds the hosted service with its scope factory and logger.</summary>
    public IdempotencyCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<IdempotencyCleanupService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarting(SweepInterval);

        // Run once at startup so a freshly booted host doesn't have to wait an
        // hour to drain whatever the previous instance left behind.
        await SweepOnceAsync(stoppingToken).ConfigureAwait(false);

        using PeriodicTimer timer = new(SweepInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool tick = await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
                if (!tick)
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await SweepOnceAsync(stoppingToken).ConfigureAwait(false);
        }

        LogStopping();
    }

    private async Task SweepOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            IdempotencyKeyCleanupRepository repo = scope.ServiceProvider
                .GetRequiredService<IdempotencyKeyCleanupRepository>();
            await repo.RemoveExpiredAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            // Background sweeps must never crash the host: log and keep looping.
            LogSweepError(ex);
        }
    }

    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Information,
        Message = "IdempotencyCleanupService starting; sweep interval is {Interval}.")]
    private partial void LogStarting(TimeSpan interval);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Information,
        Message = "IdempotencyCleanupService stopping (host cancellation requested).")]
    private partial void LogStopping();

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Error,
        Message = "IdempotencyCleanupService sweep failed; will retry on next tick.")]
    private partial void LogSweepError(Exception ex);
}
