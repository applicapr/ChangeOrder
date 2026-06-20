using ChangeOrder.Business.Abstractions;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.Errors;

namespace ChangeOrder.Business.Commands.SetApproval;

/// <summary>
/// Handler para registrar el veredicto de un nivel de aprobación en la cadena
/// </summary>

public sealed class SetApprovalHandler : ICommandHandler<SetApprovalCommand, Guid>
{
    private readonly IChangeOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Inicializa el handler con sus dependencias
    /// </summary>

    public SetApprovalHandler(
        IChangeOrderRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Ejecuta la actualización del nivel de aprobación correspondiente
    /// Retorna error si la orden no existe
    /// </summary>

    public async Task<Result<Guid, Error>> HandleAsync(
        SetApprovalCommand command,
        CancellationToken ct)
    {
        ChangeOrderEntity? order = await _repository.GetByIdAsync(command.Id, ct);

        if (order is null)
            return Result<Guid, Error>.Failure(DomainErrors.Order.NotFound);

        ApprovalStatus verdict = Enum.Parse<ApprovalStatus>(command.Verdict);

        order.Approval = command.Level switch
        {
            "requester" => order.Approval with { RequesterApproval = verdict },
            "departmentHead" => order.Approval with { DepartmentHeadApproval = verdict },
            "itHead" => order.Approval with { ItHeadApproval = verdict },
            "programmingDivision" => order.Approval with { ProgrammingDivisionApproval = verdict },
            _ => order.Approval
        };

        _repository.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<Guid, Error>.Success(order.Id);
    }
}
