using ChangeOrder.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChangeOrder.Host.BackgroundServices;

/// <summary>
/// Servicio en segundo plano que elimina periódicamente los registros de idempotencia
/// con más de 24 horas de antigüedad para evitar el crecimiento indefinido de la tabla.
/// Se ejecuta cada hora mientras la aplicación esté corriendo.
/// </summary>
public sealed class IdempotencyCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdempotencyCleanupService> _logger;

    /// <summary>
    /// Inicializa el servicio con sus dependencias.
    /// </summary>
    public IdempotencyCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<IdempotencyCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el ciclo de limpieza cada hora mientras la aplicación esté corriendo.
    /// El loop termina limpiamente cuando <paramref name="stoppingToken"/> es cancelado.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromHours(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Crea un scope de DI, resuelve el repositorio y elimina los registros viejos.
    /// Las excepciones se capturan para que un tick fallido no tumbe el servicio completo.
    /// OperationCanceledException se deja propagar para que el loop termine al apagar la app.
    /// </summary>
    private async Task CleanupAsync(CancellationToken ct)
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

            IChangeOrderRepository repository = scope.ServiceProvider
                .GetRequiredService<IChangeOrderRepository>();

            DateTime cutoff = DateTime.UtcNow.AddHours(-24);
            int deleted = await repository.DeleteOldIdempotencyRecordsAsync(cutoff, ct);

            _logger.LogInformation(
                "Limpieza de idempotencia: {Count} registro(s) eliminado(s) (anteriores a {Cutoff:O}).",
                deleted, cutoff);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error durante la limpieza de registros de idempotencia.");
        }
    }
}
