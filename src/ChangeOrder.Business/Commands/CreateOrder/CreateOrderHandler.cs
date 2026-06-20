using ChangeOrder.Business.Abstractions;
using ChangeOrder.Business.Services;
using ChangeOrder.Business.Validation;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Domain.ValueObjects;

namespace ChangeOrder.Business.Commands.CreateOrder;

/// <summary>
/// Handler para crear una nueva orden de cambio
/// </summary>

public sealed class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    private readonly IChangeOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderNumberGenerator _generator;


    /// <summary>
    /// Inicializa el handler con sus dependencias
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
    /// Ejecuta la creación de la orden — genera el número, crea la entidad y persist.
    /// Aplica idempotencia: si la misma IdempotencyKey ya fue usada con el mismo
    /// payload, retorna el resultado original sin duplicar la orden.
    /// </summary>

    public async Task<Result<Guid, Error>> HandleAsync(
    CreateOrderCommand command,
    CancellationToken ct)
    {
        List<string> validationErrors = CreateOrderValidator.Validate(command);
        if (validationErrors.Count > 0)
            return Result<Guid, Error>.Failure(DomainErrors.Order.ValidationFailed);

        // 0. Verificar idempotencia
        string payloadHash = IdempotencyService.ComputeHash(BuildPayloadString(command));

        IdempotencyRecord? existingRecord =
            await _repository.GetIdempotencyRecordAsync(command.IdempotencyKey, ct);

        if (existingRecord is not null)
        {
            if (existingRecord.PayloadHash != payloadHash)
                return Result<Guid, Error>.Failure(DomainErrors.Order.IdempotencyKeyConflict);

            return Result<Guid, Error>.Success(existingRecord.ResourceId);
        }

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

        // 5. Guardar el registro de idempotencia
        IdempotencyRecord record = new()
        {
            Key = command.IdempotencyKey,
            PayloadHash = payloadHash,
            ResourceId = order.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddIdempotencyRecordAsync(record, ct);

        // 6. Confirmar transacción
        await _unitOfWork.SaveChangesAsync(ct);

        // 7. Retornar éxito
        return Result<Guid, Error>.Success(order.Id);
    }

    /// <summary>
    /// Construye una representación canónica del command para el cálculo del hash.
    /// </summary>
    private static string BuildPayloadString(CreateOrderCommand command) =>
        $"{command.ProgramName}|{command.ProductionVersion}|{command.RequestDate:O}|" +
        $"{command.WorkDescription}|{command.RequestDetails}|{command.Justification}|" +
        $"{command.RequiredAction}|{command.RequesterName}|{command.RequesterPosition}|" +
        $"{command.RequesterDepartment}|{command.RequesterEmail}";
}
