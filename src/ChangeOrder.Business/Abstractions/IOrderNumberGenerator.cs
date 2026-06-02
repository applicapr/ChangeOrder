using ChangeOrder.Domain.ValueObjects;

namespace ChangeOrder.Business.Abstractions;

/// <summary>
/// Contrato para el generador de números de orden.
/// </summary>
public interface IOrderNumberGenerator
{
    /// <summary>
    /// Genera el próximo número de orden para la fecha indicada.
    /// </summary>
    public Task<OrderNumber> GenerateAsync(DateTime date, CancellationToken ct);
}
