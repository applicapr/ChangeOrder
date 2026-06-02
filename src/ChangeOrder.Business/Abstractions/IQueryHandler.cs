using ChangeOrder.Domain.Errors;

namespace ChangeOrder.Business.Abstractions;

/// <summary>
/// Contrato genérico para handlers de Queries — operaciones de solo lectura
/// </summary>

public interface IQueryHandler<TQuery, TResponse>
{
    /// <summary>
    /// Ejecuta la consulta y retorna un resultado de éxito o error.
    /// </summary>
    public Task<Result<TResponse, Error>> HandleAsync(TQuery query, CancellationToken ct);
}
