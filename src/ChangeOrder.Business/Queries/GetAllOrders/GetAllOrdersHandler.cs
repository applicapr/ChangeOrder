using ChangeOrder.Business.Abstractions;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;

namespace ChangeOrder.Business.Queries.GetAllOrders;

/// <summary>
/// Handler para obtener todas las órdenes de cambio de forma paginada
/// </summary>
public sealed class GetAllOrdersHandler : IQueryHandler<GetAllOrdersQuery, IReadOnlyList<ChangeOrderEntity>>
{
    private readonly IChangeOrderRepository _repository;

    /// <summary>
    /// Inicializa el handler con el repositorio 
    /// </summary>

    public GetAllOrdersHandler(IChangeOrderRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Ejecuta la consulta y retorna la lista de órdenes.
    /// Pendiente: implementar paginación cuando GetAllAsync esté disponible
    /// </summary>

    public async Task<Result<IReadOnlyList<ChangeOrderEntity>, Error>> HandleAsync(
    GetAllOrdersQuery query, CancellationToken ct)
    {
        // TODO: Implementar cuando GetAllAsync esté disponible
        IReadOnlyList<ChangeOrderEntity> orders = await _repository
            .GetByDateAsync(DateTime.UtcNow, ct);
        return Result<IReadOnlyList<ChangeOrderEntity>, Error>.Success(orders);
    }
}
