using ChangeOrder.Business.Abstractions;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.ValueObjects;

namespace ChangeOrder.Business.Services;

/// <summary>
/// Servicio que genera el número de orden con formato yyyyMMdd-##.
/// </summary>

public sealed class OrderNumberGenerator : IOrderNumberGenerator
{
    private readonly IChangeOrderRepository _repository;

    /// <summary>
    /// Inicializa el generador con el repositorio de órdenes.
    /// </summary>

    public OrderNumberGenerator(IChangeOrderRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Genera el próximo número de orden para la fecha indicada
    /// </summary>

    public async Task<OrderNumber> GenerateAsync(DateTime date, CancellationToken ct)
    {
        int sequence = await _repository.GetNextSequenceForDateAsync(date, ct);
        return OrderNumber.Create(date, sequence);
    }
}
