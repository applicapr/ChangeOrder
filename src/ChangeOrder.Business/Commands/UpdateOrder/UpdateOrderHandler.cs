    using ChangeOrder.Business.Abstractions;
    using ChangeOrder.Domain.Abstractions;
    using ChangeOrder.Domain.Entities;
    using ChangeOrder.Domain.Errors;

    namespace ChangeOrder.Business.Commands.UpdateOrder;

    /// <summary>
    /// Handler para actualizar una orden de cambio existente 
    /// </summary>

    public sealed class UpdateOrderHandler : ICommandHandler<UpdateOrderCommand, Guid>
    {
        private readonly IChangeOrderRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Inicializa el handler con sus dependencias.
        /// </summary>

        public UpdateOrderHandler(
            IChangeOrderRepository repository,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Ejecuta la actualización de la orden. Retorna error si la orden no existe
        /// </summary>


        public async Task<Result<Guid, Error>> HandleAsync(
        UpdateOrderCommand command,
        CancellationToken ct)
        {
            ChangeOrderEntity? order = await _repository.GetByIdAsync(command.Id, ct);

            if (order is null)
                return Result<Guid, Error>.Failure(DomainErrors.Order.NotFound);

            order.ProgramName = command.ProgramName;
            order.ProductionVersion = command.ProductionVersion;
            order.VersionScreenshotPath = command.VersionScreenshotPath;
            order.WorkDescription = command.WorkDescription;
            order.RequestDetails = command.RequestDetails;
            order.Justification = command.Justification;
            order.RequiredAction = command.RequiredAction;
            order.DeliveryDate = command.DeliveryDate;
            order.Status = command.Status;
            _repository.UpdateWithConcurrencyToken(order, command.RowVersion);

            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (ConcurrencyException)
            {
                return Result<Guid, Error>.Failure(DomainErrors.Order.ConcurrencyConflict);
            }

            return Result<Guid, Error>.Success(order.Id);
        }
    }
