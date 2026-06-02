using ChangeOrder.Domain.Errors;

namespace ChangeOrder.Business.Abstractions;

/// <summary>
/// Contrato genérico para handlers de Commands — operaciones que modifican el sistema.
/// </summary>

public interface ICommandHandler<TCommand , TResponse>
{
    /// <summary>
    /// Ejecuta el command y retorna un resultado de éxito o error.
    /// </summary>

    public Task<Result<TResponse, Error>> HandleAsync(TCommand command, CancellationToken ct);

}
