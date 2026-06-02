using ChangeOrder.Business.Abstractions;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;

namespace ChangeOrder.Business.Commands.DeleteOrder;

/// <summary>
/// Handler para eliminar lógicamente una orden de cambio
/// </summary>

public sealed class DeleteOrderHandler : ICommandHandler<DeleteOrderCommand, Guid>
{
    private readonly IChangeOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Inicializa el handler con sus dependencias
    /// </summary>

    public DeleteOrderHandler(
        IChangeOrderRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Ejecuta el borrado lógico de la orden. Retorna error si la orden no existe
    /// </summary>

    public async Task<Result<Guid, Error>> HandleAsync(
        DeleteOrderCommand command,
        CancellationToken ct)
    {
        ChangeOrderEntity? order = await _repository.GetByIdAsync(command.Id, ct);
        if (order is null)
            return Result<Guid, Error>.Failure(DomainErrors.Order.NotFound);

        _repository.Delete(order);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<Guid, Error>.Success(order.Id);
    }
}
