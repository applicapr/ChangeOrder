using ChangeOrder.Business.Abstractions;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.Errors;

namespace ChangeOrder.Business.Commands.SetOrderDates;

/// <summary>
/// Handler para actualizar las fechas de seguimiento de una orden y disparar transiciones de estado
/// </summary>
public sealed class SetOrderDatesHandler : ICommandHandler<SetOrderDatesCommand, Guid>
{
    private readonly IChangeOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Inicializa el handler con sus dependencias
    /// </summary>
    public SetOrderDatesHandler(
        IChangeOrderRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Ejecuta la actualización de fechas y las transiciones de estado correspondientes
    /// </summary>
    public async Task<Result<Guid, Error>> HandleAsync(
        SetOrderDatesCommand command,
        CancellationToken ct)
    {
        ChangeOrderEntity? order = await _repository.GetByIdAsync(command.Id, ct);

        if (order is null)
            return Result<Guid, Error>.Failure(DomainErrors.Order.NotFound);

        if (command.InitialEvaluationDate is not null)
        {
            order.InitialEvaluationDate = command.InitialEvaluationDate;
        }

        if (command.DeliveryDate is not null)
        {
            order.DeliveryDate = command.DeliveryDate;
            order.Status = OrderStatus.InProgress;
        }

        if (command.ProductionDeployDate is not null)
        {
            order.ProductionDeployDate = command.ProductionDeployDate;
            order.Status = OrderStatus.Deployed;
        }

        _repository.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<Guid, Error>.Success(order.Id);
    }
}
