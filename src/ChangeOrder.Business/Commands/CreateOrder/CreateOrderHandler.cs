using ChangeOrder.Business.Abstractions;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Domain.ValueObjects;

namespace ChangeOrder.Business.Commands.CreateOrder;

/// <summary>
/// 
/// </summary>

public sealed class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    private readonly IChangeOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderNumberGenerator _generator;


    /// <summary>
    /// 
    /// </summary>

    public CreateOrderHandler(

        IChangeOrderRepository repository,
        IUnitOfWork unitOfWork,
        IOrderNumberGenerator generator)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _generator = generator;
    }

    /// <summary>
    /// 
    /// </summary>
    
    public async Task<Result<Guid, Error>> HandleAsync(
    CreateOrderCommand command,
    CancellationToken ct)
    {
        // 1. Generar OrderNumber
        OrderNumber number = await _generator.GenerateAsync(command.RequestDate, ct);

        // 2. Crear RequesterInfo
        RequesterInfo requester = new(
            command.RequesterName,
            command.RequesterPosition,
            command.RequesterDepartment,
            command.RequesterEmail);

        // 3. Crear la entidad
        ChangeOrderEntity order = new()
        {
            Id = Guid.NewGuid(),
            Number = number,
            ProgramName = command.ProgramName,
            ProductionVersion = command.ProductionVersion,
            VersionScreenshotPath = command.VersionScreenshotPath,
            RequestDate = command.RequestDate,
            WorkDescription = command.WorkDescription,
            RequestDetails = command.RequestDetails,
            Justification = command.Justification,
            RequiredAction = command.RequiredAction,
            Requester = requester,
            Approval = new ApprovalChain(),
            Status = OrderStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        // 4. Guardar
        await _repository.AddAsync(order, ct);

        // 5. Confirmar transacción
        await _unitOfWork.SaveChangesAsync(ct);

        // 6. Retornar éxito
        return Result<Guid, Error>.Success(order.Id);
    }

}
