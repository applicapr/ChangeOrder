using ChangeOrder.Business.Abstractions;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;

namespace ChangeOrder.Business.Queries.GetOrdersByDate;

/// <summary>
/// Handler para obtener las órdenes de cambio de una fecha específica
/// </summary>

public sealed class GetOrdersByDateHandler : IQueryHandler<GetOrdersByDateQuery, IReadOnlyList<ChangeOrderEntity>>
{
    private readonly IChangeOrderRepository _repository;

    /// <summary>
    /// Inicializa el handler con el repositorio
    /// </summary>

    public GetOrdersByDateHandler(IChangeOrderRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Ejecuta la consulta y retorna las órdenes de la fecha indicada
    /// </summary>

    public async Task<Result<IReadOnlyList<ChangeOrderEntity>, Error>> HandleAsync(
        GetOrdersByDateQuery query, CancellationToken ct)
    {
        IReadOnlyList<ChangeOrderEntity> orders = await _repository
            .GetByDateAsync(query.Date, ct);

        return Result<IReadOnlyList<ChangeOrderEntity>, Error>.Success(orders);
    }
}
