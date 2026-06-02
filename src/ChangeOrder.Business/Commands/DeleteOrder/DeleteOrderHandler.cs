using ChangeOrder.Business.Abstractions;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;

namespace ChangeOrder.Business.Commands.DeleteOrder;

/// <summary>
/// 
/// </summary>

public sealed class DeleteOrderHandler : ICommandHandler<DeleteOrderCommand, Guid>
{
    private readonly IChangeOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// 
    /// </summary>

    public DeleteOrderHandler(
        IChangeOrderRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// 
    /// </summary>

    public async Task<Result<Guid, Error>> HandleAsync(
        DeleteOrderCommand command,
        CancellationToken ct)
    {
        ChangeOrderEntity? order = await _repository.GetByIdAsync(command.Id, ct);
        if (order == null)
            return Result<Guid, Error>.Failure(DomainErrors.Order.NotFound);

        _repository.Delete(order);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<Guid, Error>.Success(order.Id);
    }
}
