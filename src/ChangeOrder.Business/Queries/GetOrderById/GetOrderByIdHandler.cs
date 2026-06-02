using ChangeOrder.Business.Abstractions;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;

namespace ChangeOrder.Business.Queries.GetOrderById;

/// <summary>
/// Handler para obtener una orden de cambio por su Id 
/// </summary>

public sealed class GetOrderByIdHandler : IQueryHandler<GetOrderByIdQuery, ChangeOrderEntity>
{
    private readonly IChangeOrderRepository _repository;

    /// <summary>
    /// Inicializa el handler con el repositorio 
    /// </summary>

    public GetOrderByIdHandler(IChangeOrderRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Ejecuta la consulta y retorna la orden si existe, o un error si no se encuentra 
    /// </summary>

    public async Task<Result<ChangeOrderEntity, Error>> HandleAsync(GetOrderByIdQuery query, CancellationToken ct)
    {
        ChangeOrderEntity? order = await _repository.GetByIdAsync(query.Id, ct);
        if (order is null)
            return Result<ChangeOrderEntity, Error>.Failure(DomainErrors.Order.NotFound);

        return Result<ChangeOrderEntity, Error>.Success(order);
    }
}
